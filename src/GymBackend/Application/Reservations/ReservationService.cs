using GymBackend.Application.Common;
using GymBackend.Data;
using GymBackend.Domain;
using GymBackend.DTOs;
using GymBackend.Models;
using GymBackend.Options;
using GymBackend.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GymBackend.Application.Reservations;

public sealed class ReservationService(AppDbContext context, IGymClock clock, IOptions<ReservationOptions> options) : IReservationService
{
    public async Task<ServiceResult<ReservationResponse[]>> GetAllAsync(CancellationToken ct = default)
    {
        var reservations = await context.Reservations.Include(r => r.User).Include(r => r.ScheduleSlot)
            .OrderByDescending(reservation => reservation.Date)
            .ThenByDescending(reservation => reservation.ScheduleSlot.StartTime)
            .ToListAsync(ct);

        return ServiceResult<ReservationResponse[]>.Success(reservations.Select(ReservationResponse.From).ToArray());
    }

    public async Task<ServiceResult<ReservationResponse[]>> GetByUserAsync(int requestedUserId, int? currentUserId, bool isAdmin, CancellationToken ct = default)
    {
        if (!isAdmin)
        {
            if (currentUserId is null)
            {
                return ServiceError.Unauthorized("La sesión no contiene un usuario válido.");
            }

            if (currentUserId.Value != requestedUserId)
            {
                return ServiceError.Forbidden();
            }
        }

        var reservations = await context.Reservations.Include(r => r.User).Include(r => r.ScheduleSlot)
            .Where(reservation => reservation.UserId == requestedUserId)
            .OrderByDescending(reservation => reservation.Date)
            .ThenByDescending(reservation => reservation.ScheduleSlot.StartTime)
            .ToListAsync(ct);

        return ServiceResult<ReservationResponse[]>.Success(reservations.Select(ReservationResponse.From).ToArray());
    }

    public async Task<ServiceResult<ReservationMessageResponse>> CreateAsync(int userId, CreateReservationRequest request, CancellationToken ct = default)
    {
        await using var transaction = context.Database.CurrentTransaction is null ? await context.Database.BeginTransactionAsync(ct) : null;
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE", ct);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"ScheduleSlots\" WHERE \"Id\" = {request.ScheduleSlotId} FOR UPDATE", ct);

        var schedule = await context.ScheduleSlots.FirstOrDefaultAsync(slot => slot.Id == request.ScheduleSlotId && slot.Enabled, ct);
        if (schedule is null)
            return ServiceError.Validation("El bloque seleccionado no está disponible.");
        if (request.Date.DayOfWeek != schedule.Day)
            return ServiceError.Validation("La fecha no corresponde al día del bloque.");
        var now = clock.LocalNow;
        var today = DateOnly.FromDateTime(now.DateTime);
        if (request.Date < today || (request.Date == today && TimeOnly.FromDateTime(now.DateTime) >= schedule.StartTime))
            return ServiceError.Validation("El bloque ya comenzó o la fecha ya pasó.");
        var user = await context.Users.FindAsync(new object[] { userId }, ct);
        if (user is null)
            return ServiceError.Unauthorized("La sesión no corresponde a un usuario válido.");
        var maxDaysAhead = user.Category is { } category && options.Value.CategoryMaxDaysAhead.TryGetValue(category, out var days)
            ? days : options.Value.MaxDaysAhead;
        if (request.Date > BookingWindow.LastAllowedDate(today, maxDaysAhead, options.Value.HorizonUnit))
            return ServiceError.Validation("La fecha excede el plazo máximo de reserva.");

        var reservedCount = await context.Reservations.CountAsync(reservation =>
            reservation.ScheduleSlotId == schedule.Id && reservation.Date == request.Date &&
            (reservation.Status == ReservationStatus.Reserved || reservation.Status == ReservationStatus.Attended), ct);

        reservedCount += await context.GroupBookings.Where(g => g.ScheduleSlotId == schedule.Id && g.Date == request.Date
            && g.Status == "Activa").SumAsync(g => g.Seats, ct);

        if (!ReservationRules.HasAvailableCapacity(reservedCount, schedule.Capacity))
        {
            return ServiceError.Conflict("No quedan cupos disponibles para este bloque.");
        }

        var alreadyReserved = await context.Reservations.AnyAsync(reservation =>
            reservation.UserId == userId &&
            reservation.ScheduleSlotId == schedule.Id && reservation.Date == request.Date &&
            (reservation.Status == ReservationStatus.Reserved || reservation.Status == ReservationStatus.Attended), ct);

        if (alreadyReserved)
        {
            return ServiceError.Validation("Ya tienes una reserva para este bloque.");
        }

        var dailyCount = await context.Reservations.CountAsync(r => r.UserId == userId && r.Date == request.Date
            && (r.Status == ReservationStatus.Reserved || r.Status == ReservationStatus.Attended), ct);
        if (!ReservationRules.CanBookOnDate(user.Category, dailyCount))
            return ServiceError.Conflict("Los estudiantes pueden reservar como m\u00e1ximo una sesi\u00f3n por d\u00eda.");

        var reservation = new Reservation
        {
            CategoryAtBooking = user.Category,
            SportBranchIdAtBooking = user.SportBranchId,
            UserId = user.Id,
            User = user,
            ScheduleSlotId = schedule.Id,
            ScheduleSlot = schedule,
            Date = request.Date,
            Status = ReservationStatus.Reserved,
            CreatedAt = clock.UtcNow.UtcDateTime,
        };

        context.Reservations.Add(reservation);
        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            return ServiceError.Conflict("Ya tienes una reserva para este bloque.");
        }

        if (transaction is not null) await transaction.CommitAsync(ct);

        return ServiceResult<ReservationMessageResponse>.Success(new ReservationMessageResponse("Reserva creada correctamente.", ReservationResponse.From(reservation)));
    }

    public async Task<ServiceResult<ReservationMessageResponse>> CancelAsync(int id, int? currentUserId, bool isAdmin, CancellationToken ct = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var target = await context.Reservations.AsNoTracking().Where(r => r.Id == id)
            .Select(r => new { r.UserId, r.ScheduleSlotId }).SingleOrDefaultAsync(ct);
        if (target is null) return ServiceError.NotFound("Reserva no encontrada.");
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Users\" WHERE \"Id\" = {target.UserId} FOR UPDATE", ct);
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"ScheduleSlots\" WHERE \"Id\" = {target.ScheduleSlotId} FOR UPDATE", ct);
        var reservation = await context.Reservations.Include(r => r.User).Include(r => r.ScheduleSlot).FirstOrDefaultAsync(r => r.Id == id, ct);

        if (reservation is null)
        {
            return ServiceError.NotFound("Reserva no encontrada.");
        }

        if (!isAdmin)
        {
            if (currentUserId is null)
            {
                return ServiceError.Unauthorized("La sesión no contiene un usuario válido.");
            }

            if (reservation.UserId != currentUserId.Value)
            {
                return ServiceError.Forbidden();
            }
        }

        if (reservation.Status != ReservationStatus.Reserved)
            return ServiceError.Validation("Solo se pueden cancelar reservas activas.");
        var now = clock.LocalNow;
        var today = DateOnly.FromDateTime(now.DateTime);
        if (!isAdmin && (reservation.Date < today || (reservation.Date == today && TimeOnly.FromDateTime(now.DateTime) >= reservation.ScheduleSlot.StartTime)))
            return ServiceError.Validation("La reserva ya no se puede cancelar porque el bloque comenzó.");

        reservation.Status = ReservationStatus.Cancelled;
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return ServiceResult<ReservationMessageResponse>.Success(new ReservationMessageResponse("Reserva cancelada correctamente.", ReservationResponse.From(reservation)));
    }
}

