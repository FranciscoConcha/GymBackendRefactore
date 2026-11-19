using GymBackend.Application.Common;
using GymBackend.Application.Groups;
using GymBackend.Application.Qr;
using GymBackend.Data;
using GymBackend.Domain;
using GymBackend.DTOs;
using GymBackend.Models;
using GymBackend.Options;
using GymBackend.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GymBackend.Application.Access;

public sealed class AccessService(AppDbContext db, IQrTokenService tokens, IGymClock clock, IOptions<GymOptions> options)
{
    // All entry/exit writers use this transaction-scoped lock. It also serializes group seat consumption.
    private const long OccupancyLock = 741028365;

    public async Task<ServiceResult<AccessValidationResponse>> ValidateAsync(string? token, int operatorId, CancellationToken ct)
    {
        var (user, error) = await ResolveAsync(token, ct);
        if (user is null)
        {
            await RecordAsync("Validar", operatorId, null, error, ct);
            return ServiceError.Validation(error);
        }
        if (tokens.TryRead(token, out var exitPayload) && exitPayload.Kind == "S")
        {
            var visit = await db.Visits.SingleOrDefaultAsync(v => v.Id == exitPayload.VisitId && v.UserId == user.Id, ct);
            var exitReason = ExitTokenRejection(exitPayload, visit);
            var occupancy = await db.Visits.CountAsync(v => v.ExitAt == null, ct);
            var isInside = await db.Visits.AnyAsync(v => v.UserId == user.Id && v.ExitAt == null, ct);
            await RecordAsync("Validar", operatorId, user.Id, exitReason, ct);
            return ServiceResult<AccessValidationResponse>.Success(new(AccessUserResponse.From(user), null, isInside,
                false, exitReason, new(occupancy, options.Value.MaxOccupancy), "Salida", exitReason is null));
        }
        var session = await FindSessionAsync(user, token, ct);
        var inside = await db.Visits.AnyAsync(v => v.UserId == user.Id && v.ExitAt == null, ct);
        var current = await db.Visits.CountAsync(v => v.ExitAt == null, ct);
        var reason = AccessRules.Rejection(user.Enabled, user.AccountStatus == AccountStatus.Activa,
            inside, session is not null, current, options.Value.MaxOccupancy);
        if (user.Enabled && user.AccountStatus == AccountStatus.Activa && tokens.TryRead(token, out var payload) && !payload.IsCurrent(clock.UtcNow))
            reason = AccessQrToken.NotCurrent;
        await RecordAsync("Validar", operatorId, user.Id, reason, ct);
        return ServiceResult<AccessValidationResponse>.Success(new(AccessUserResponse.From(user), session, inside,
            reason is null, reason, new(current, options.Value.MaxOccupancy)));
    }

    public async Task<ServiceResult<VisitMessageResponse>> EntryAsync(AccessRequest request, int operatorId, CancellationToken ct)
    {
        await using var transaction = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({OccupancyLock})", ct);
        var (user, error) = await ResolveAsync(request.Token, ct);
        AccessSessionResponse? session = null;
        string? reason = error;
        var conflict = false;
        if (user is not null)
        {
            // Account changes cannot race entry validation.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Users\" WHERE \"Id\" = {user.Id} FOR UPDATE", ct);
            await db.Entry(user).ReloadAsync(ct);
            session = await FindSessionAsync(user, request.Token, ct);
            if (session is not null)
            {
                var slotId = session.ReservationId is { } rid
                    ? await db.Reservations.Where(r => r.Id == rid).Select(r => r.ScheduleSlotId).SingleAsync(ct)
                    : await db.GroupBookings.Where(g => g.Id == session.GroupBookingId).Select(g => g.ScheduleSlotId).SingleAsync(ct);
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"ScheduleSlots\" WHERE \"Id\" = {slotId} FOR UPDATE", ct);
                // Recheck cancellation and the window after waiting for the slot lock.
                session = await FindSessionAsync(user, request.Token, ct);
            }
            var inside = await db.Visits.AnyAsync(v => v.UserId == user.Id && v.ExitAt == null, ct);
            var current = await db.Visits.CountAsync(v => v.ExitAt == null, ct);
            reason = AccessRules.Rejection(user.Enabled, user.AccountStatus == AccountStatus.Activa, inside,
                session is not null, current, options.Value.MaxOccupancy);
            if (user.Enabled && user.AccountStatus == AccountStatus.Activa && tokens.TryRead(request.Token, out var payload) && !payload.IsCurrent(clock.UtcNow))
                reason = AccessQrToken.NotCurrent;
            conflict = reason == "El usuario ya se encuentra dentro de la sala." || reason == AccessRules.Full;
            if (reason is null && !AccessRules.ValidDocument(request.DocumentType))
                reason = "Debe confirmar un documento válido: CarnetIdentidad, TNE o CredencialUniversitaria.";
        }
        if (reason is not null)
        {
            await RecordAsync("Entrada", operatorId, user?.Id, reason, ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return conflict ? ServiceError.Conflict(reason) : ServiceError.Validation(reason);
        }
        var visit = new Visit
        {
            UserId = user!.Id,
            User = user,
            ReservationId = session!.ReservationId,
            GroupBookingId = session.GroupBookingId,
            Date = DateOnly.ParseExact(session.Date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            EntryAt = clock.UtcNow.UtcDateTime,
            EntryOperatorId = operatorId,
            EntryOperator = await db.Users.SingleAsync(u => u.Id == operatorId, ct),
            DocumentType = request.DocumentType!,
        };
        db.Visits.Add(visit);
        if (session.ReservationId is { } reservationId)
        {
            var reservation = await db.Reservations.SingleAsync(r => r.Id == reservationId, ct);
            reservation.Status = ReservationStatus.Attended;
        }
        await RecordAsync("Entrada", operatorId, user.Id, null, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return ServiceResult<VisitMessageResponse>.Success(new("Entrada registrada correctamente.", VisitResponse.From(visit)));
    }

    public async Task<ServiceResult<VisitMessageResponse>> ExitAsync(ExitAccessRequest request, int operatorId, CancellationToken ct)
    {
        if ((request.Token is null) == (request.UserId is null))
        {
            const string invalidRequest = "Indique solo token o userId.";
            await RecordAsync("Salida", operatorId, null, invalidRequest, ct);
            return ServiceError.Validation(invalidRequest);
        }
        await using var transaction = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({OccupancyLock})", ct);
        Visit? visit;
        if (request.Token is not null)
        {
            string? rejection;
            if (!tokens.TryRead(request.Token, out var payload)) rejection = AccessQrToken.Invalid;
            else if (payload.Kind != "S") rejection = "Escanee el QR de salida.";
            else rejection = null;
            visit = rejection is null
                ? await Visits().SingleOrDefaultAsync(v => v.Id == payload.VisitId && v.UserId == payload.UserId, ct) : null;
            rejection ??= ExitTokenRejection(payload, visit);
            if (rejection is not null)
            {
                await RecordAsync("Salida", operatorId, visit?.UserId, rejection, ct);
                if (transaction is not null) await transaction.CommitAsync(ct);
                return ServiceError.Validation(rejection);
            }
        }
        else visit = await Visits().SingleOrDefaultAsync(v => v.UserId == request.UserId && v.ExitAt == null, ct);
        if (visit is null)
        {
            const string reason = "El usuario no tiene una visita abierta.";
            await RecordAsync("Salida", operatorId, null, reason, ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return ServiceError.Validation(reason);
        }
        visit.ExitAt = clock.UtcNow.UtcDateTime;
        visit.ExitOperatorId = operatorId;
        visit.ExitOperator = await db.Users.SingleAsync(u => u.Id == operatorId, ct);
        await RecordAsync("Salida", operatorId, visit.UserId, request.Token is null ? "Manual" : "QR", ct, accepted: true);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return ServiceResult<VisitMessageResponse>.Success(new("Salida registrada correctamente.", VisitResponse.From(visit)));
    }

    public async Task<CurrentAccessResponse> CurrentAsync(CancellationToken ct)
    {
        var visits = await Visits().Include(v => v.Reservation!).ThenInclude(r => r.ScheduleSlot)
            .Include(v => v.GroupBooking!).ThenInclude(g => g.ScheduleSlot).Where(v => v.ExitAt == null)
            .OrderBy(v => v.EntryAt).ToArrayAsync(ct);
        return new(new(visits.Length, options.Value.MaxOccupancy), visits.Select(v => new CurrentPersonResponse(v.UserId,
            v.User.FullName, v.User.Rut, v.User.Category, v.EntryAt,
            (v.Reservation?.ScheduleSlot ?? v.GroupBooking!.ScheduleSlot).EndTime.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture))).ToArray());
    }

    public async Task<VisitResponse[]> HistoryAsync(DateOnly? from, DateOnly? to, int? userId, int? branchId, string? category, CancellationToken ct)
    {
        var query = Visits().Where(v => (!from.HasValue || v.Date >= from) && (!to.HasValue || v.Date <= to)
            && (!userId.HasValue || v.UserId == userId));
        if (branchId.HasValue) query = query.Where(v => v.Reservation != null
            ? v.Reservation.SportBranchIdAtBooking == branchId : v.GroupBooking!.SportBranchId == branchId);
        if (category is not null) query = query.Where(v => v.Reservation != null
            ? v.Reservation.CategoryAtBooking == category : category == UserCategories.Deportista);
        return (await query.OrderByDescending(v => v.EntryAt).ToArrayAsync(ct)).Select(VisitResponse.From).ToArray();
    }

    private string? ExitTokenRejection(AccessQrPayload payload, Visit? visit)
    {
        if (visit is null) return AccessQrToken.Invalid;
        if (visit.ExitAt is not null) return "Este QR de salida ya fue utilizado.";
        var entry = new DateTimeOffset(DateTime.SpecifyKind(visit.EntryAt, DateTimeKind.Utc));
        if (payload.NotBefore != entry.ToUnixTimeSeconds() || payload.Expires != entry.AddHours(18).ToUnixTimeSeconds())
            return AccessQrToken.Invalid;
        return payload.IsCurrent(clock.UtcNow) ? null : AccessQrToken.NotCurrent;
    }

    private IQueryable<Visit> Visits() => db.Visits.Include(v => v.User).Include(v => v.EntryOperator).Include(v => v.ExitOperator);

    private async Task<(User? User, string Error)> ResolveAsync(string? token, CancellationToken ct)
    {
        if (!tokens.TryRead(token, out var payload)) return (null, AccessQrToken.Invalid);
        var user = await db.Users.Include(u => u.SportBranch).SingleOrDefaultAsync(u => u.Id == payload.UserId, ct);
        return (user, user is null ? AccessQrToken.Invalid : string.Empty);
    }

    private async Task<AccessSessionResponse?> FindSessionAsync(User user, string? token, CancellationToken ct)
    {
        if (!tokens.TryRead(token, out var payload) || payload.Kind == "S" || payload.UserId != user.Id || !payload.IsCurrent(clock.UtcNow)) return null;
        if (payload.Kind == "R")
        {
            var reservation = await db.Reservations.AsNoTracking().Include(r => r.ScheduleSlot)
                .SingleOrDefaultAsync(r => r.Id == payload.ReservationId && r.UserId == user.Id &&
                    (r.Status == ReservationStatus.Reserved || r.Status == ReservationStatus.Attended), ct);
            return reservation is not null && Matches(payload, reservation.Date, reservation.ScheduleSlot)
                ? AccessSessionResponse.From(reservation) : null;
        }
        var group = await db.GroupBookings.AsNoTracking().Include(g => g.ScheduleSlot)
            .SingleOrDefaultAsync(g => g.Id == payload.GroupBookingId && g.Status == "Activa" && g.SportBranch.Enabled, ct);
        if (group is null || !Matches(payload, group.Date, group.ScheduleSlot)) return null;
        var used = await db.Visits.Where(v => v.GroupBookingId == group.Id).Select(v => v.UserId).Distinct().CountAsync(ct);
        var alreadyUsed = await db.Visits.AnyAsync(v => v.GroupBookingId == group.Id && v.UserId == user.Id, ct);
        return AccessRules.CanUseGroup(user.Category, user.SportBranchId, group.SportBranchId, group.Seats, used, alreadyUsed)
            ? AccessSessionResponse.From(group) : null;
    }

    private bool Matches(AccessQrPayload payload, DateOnly date, ScheduleSlot slot)
    {
        var start = GroupBookingRules.ToInstant(date, slot.StartTime, clock.TimeZone);
        var end = GroupBookingRules.ToInstant(date, slot.EndTime, clock.TimeZone);
        return slot.Enabled && start is not null && end is not null &&
            payload.NotBefore == start.Value.AddMinutes(-15).ToUnixTimeSeconds() && payload.Expires == end.Value.ToUnixTimeSeconds();
    }

    private async Task RecordAsync(string action, int operatorId, int? userId, string? reason, CancellationToken ct, bool accepted = false)
    {
        db.AccessEvents.Add(new AccessEvent
        {
            Action = action,
            OperatorId = operatorId,
            UserId = userId,
            OccurredAt = clock.UtcNow,
            Result = accepted || reason is null ? "Aceptado" : "Rechazado",
            Reason = reason
        });
        await db.SaveChangesAsync(ct);
    }
}
