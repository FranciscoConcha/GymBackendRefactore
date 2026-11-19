using GymBackend.Application.Audit;
using GymBackend.Application.Common;
using GymBackend.Data;
using GymBackend.Domain;
using GymBackend.DTOs;
using GymBackend.Models;
using GymBackend.Time;
using Microsoft.EntityFrameworkCore;

namespace GymBackend.Application.Groups;

public sealed class GroupBookingService(AppDbContext db, IGymClock clock, IAuditLog audit)
{
    public async Task<ServiceResult<GroupBookingMessageResponse>> CreateAsync(CreateGroupBookingRequest request,
        int operatorId, string key, CancellationToken ct)
    {
        if (request.Mode is not ("Cupos" or "TotalDisponible")) return ServiceError.Validation("Modo de reserva grupal inválido.");
        if (request.Mode == "Cupos" && request.Seats is not >= 1) return ServiceError.Validation("La cantidad de cupos debe ser mayor o igual a 1.");
        await using var transaction = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"ScheduleSlots\" WHERE \"Id\" = {request.ScheduleSlotId} FOR UPDATE", ct);
        var slot = await db.ScheduleSlots.SingleOrDefaultAsync(s => s.Id == request.ScheduleSlotId && s.Enabled, ct);
        if (slot is null) return ServiceError.Validation("El bloque seleccionado no está disponible.");
        if (request.Date.DayOfWeek != slot.Day) return ServiceError.Validation("La fecha no corresponde al día del bloque.");
        if (!GroupBookingRules.HasLeadTime(clock.UtcNow, request.Date, slot.StartTime, clock.TimeZone))
            return ServiceError.Validation("La reserva grupal requiere al menos 60 minutos de anticipación.");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"SportBranches\" WHERE \"Id\" = {request.SportBranchId} FOR UPDATE", ct);
        var branch = await db.SportBranches.SingleOrDefaultAsync(b => b.Id == request.SportBranchId && b.Enabled, ct);
        if (branch is null) return ServiceError.Validation("La rama seleccionada no está habilitada.");
        var individual = await db.Reservations.CountAsync(r => r.ScheduleSlotId == slot.Id && r.Date == request.Date
            && (r.Status == ReservationStatus.Reserved || r.Status == ReservationStatus.Attended), ct);
        var groupSeats = await db.GroupBookings.Where(g => g.ScheduleSlotId == slot.Id && g.Date == request.Date
            && g.Status == "Activa").SumAsync(g => g.Seats, ct);
        var seats = GroupBookingRules.ResolveSeats(request.Mode, request.Seats, slot.Capacity - individual - groupSeats);
        if (seats is null) return ServiceError.Conflict("No quedan cupos suficientes para este grupo.");
        var group = new GroupBooking
        {
            SportBranchId = branch.Id,
            SportBranch = branch,
            ScheduleSlotId = slot.Id,
            ScheduleSlot = slot,
            Date = request.Date,
            Mode = request.Mode,
            Seats = seats.Value,
            CreatedById = operatorId,
            CreatedBy = await db.Users.SingleAsync(u => u.Id == operatorId, ct),
            CreatedAt = clock.UtcNow,
            IdempotencyKey = key,
        };
        db.GroupBookings.Add(group);
        audit.Add("group.created", group, "sportBranchId", "scheduleSlotId", "date", "mode", "seats");
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return ServiceResult<GroupBookingMessageResponse>.Success(new("Reserva grupal creada correctamente.", GroupBookingResponse.From(group, 0)));
    }

    public async Task<ServiceResult<GroupBookingMessageResponse>> CancelAsync(int id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var slotId = await db.GroupBookings.Where(g => g.Id == id).Select(g => (int?)g.ScheduleSlotId).SingleOrDefaultAsync(ct);
        if (slotId is null) return ServiceError.NotFound("Reserva grupal no encontrada.");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"ScheduleSlots\" WHERE \"Id\" = {slotId.Value} FOR UPDATE", ct);
        var group = await Query().SingleAsync(g => g.Id == id, ct);
        if (group.Status != "Activa") return ServiceError.Conflict("La reserva grupal ya está cancelada.");
        if (group.Date.ToDateTime(group.ScheduleSlot.StartTime) <= clock.LocalNow.DateTime)
            return ServiceError.Validation("La reserva grupal ya no se puede cancelar porque el bloque comenzó.");
        group.Status = "Cancelada";
        group.CancelledAt = clock.UtcNow;
        audit.Add("group.cancelled", group, "status", "cancelledAt");
        await db.SaveChangesAsync(ct);
        var used = await UsedAsync(id, ct);
        await transaction.CommitAsync(ct);
        return ServiceResult<GroupBookingMessageResponse>.Success(new("Reserva grupal cancelada correctamente.", GroupBookingResponse.From(group, used)));
    }

    public async Task<GroupBookingResponse[]> ListAsync(DateOnly? from, DateOnly? to, int? branchId, int? currentUserId, CancellationToken ct)
    {
        var query = Query().Where(g => (!from.HasValue || g.Date >= from) && (!to.HasValue || g.Date <= to)
            && (!branchId.HasValue || g.SportBranchId == branchId));
        if (currentUserId is { } id)
        {
            var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == id, ct);
            if (user.Category != UserCategories.Deportista || user.SportBranchId is null) return [];
            query = query.Where(g => g.Status == "Activa" && g.Date >= clock.Today && g.SportBranchId == user.SportBranchId);
        }
        var groups = await query.OrderBy(g => g.Date).ThenBy(g => g.ScheduleSlot.StartTime).ToArrayAsync(ct);
        var ids = groups.Select(g => g.Id).ToArray();
        var used = await db.Visits.Where(v => v.GroupBookingId != null && ids.Contains(v.GroupBookingId.Value))
            .GroupBy(v => v.GroupBookingId!.Value).Select(g => new { Id = g.Key, Count = g.Select(v => v.UserId).Distinct().Count() })
            .ToDictionaryAsync(g => g.Id, g => g.Count, ct);
        return groups.Select(g => GroupBookingResponse.From(g, used.GetValueOrDefault(g.Id))).ToArray();
    }

    private IQueryable<GroupBooking> Query() => db.GroupBookings.Include(g => g.SportBranch).Include(g => g.ScheduleSlot).Include(g => g.CreatedBy);
    private Task<int> UsedAsync(int id, CancellationToken ct) => db.Visits.Where(v => v.GroupBookingId == id).Select(v => v.UserId).Distinct().CountAsync(ct);
}
