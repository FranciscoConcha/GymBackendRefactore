using GymBackend.Application.Common;
using GymBackend.Application.Groups;
using GymBackend.Data;
using GymBackend.Domain;
using GymBackend.DTOs;
using GymBackend.Time;
using Microsoft.EntityFrameworkCore;

namespace GymBackend.Application.Qr;

public sealed class BlockQrService(AppDbContext db, IQrTokenService tokens, IGymClock clock)
{
    public async Task<ServiceResult<BlockQrResponse>> IssueAsync(int userId, int id, bool group, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.SportBranch).SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !user.Enabled || user.AccountStatus != AccountStatus.Activa)
            return ServiceError.NotFound("Reserva no encontrada.");
        AccessSessionResponse session;
        DateOnly date;
        Models.ScheduleSlot slot;
        if (!group)
        {
            var reservation = await db.Reservations.Include(r => r.ScheduleSlot).SingleOrDefaultAsync(r => r.Id == id && r.UserId == userId &&
                (r.Status == ReservationStatus.Reserved || r.Status == ReservationStatus.Attended), ct);
            if (reservation is null || !reservation.ScheduleSlot.Enabled) return ServiceError.NotFound("Reserva no encontrada.");
            session = AccessSessionResponse.From(reservation);
            date = reservation.Date;
            slot = reservation.ScheduleSlot;
        }
        else
        {
            var booking = await db.GroupBookings.Include(g => g.ScheduleSlot).SingleOrDefaultAsync(g => g.Id == id && g.Status == "Activa"
                && g.SportBranchId == user.SportBranchId && g.SportBranch.Enabled, ct);
            if (booking is null || user.Category != UserCategories.Deportista || !booking.ScheduleSlot.Enabled)
                return ServiceError.NotFound("Grupo no encontrado.");
            session = AccessSessionResponse.From(booking);
            date = booking.Date;
            slot = booking.ScheduleSlot;
        }
        var start = GroupBookingRules.ToInstant(date, slot.StartTime, clock.TimeZone);
        var end = GroupBookingRules.ToInstant(date, slot.EndTime, clock.TimeZone);
        if (start is null || end is null || end <= start) return ServiceError.Validation("Horario de bloque inválido.");
        var from = start.Value.AddMinutes(-15);
        var payload = new AccessQrPayload(2, group ? "G" : "R", group ? null : id, group ? id : null, userId, from.ToUnixTimeSeconds(), end.Value.ToUnixTimeSeconds());
        return ServiceResult<BlockQrResponse>.Success(new(tokens.Generate(payload), from, end.Value,
            new(user.FirstName, user.LastName, user.FullName, user.Rut, user.Category,
                user.SportBranch is { } b ? new(b.Id, b.Name) : null), session));
    }
}
