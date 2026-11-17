using GymBackend.Application.Common;
using GymBackend.DTOs;
using GymBackend.Data;
using GymBackend.Domain;
using GymBackend.Models;
using GymBackend.Time;
using GymBackend.Options;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

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
        var schedule = await context.ScheduleSlots.FirstOrDefaultAsync(slot => slot.Id == request.ScheduleSlotId && slot.Enabled, ct);
        if (schedule is null)
            return ServiceError.Validation("El bloque seleccionado no está disponible.");
        if (request.Date.DayOfWeek != schedule.Day)
            return ServiceError.Validation("La fecha no corresponde al día del bloque.");
        var now = clock.LocalNow;
        var today = DateOnly.FromDateTime(now.DateTime);
        if (request.Date < today || (request.Date == today && TimeOnly.FromDateTime(now.DateTime) >= schedule.StartTime))
            return ServiceError.Validation("El bloque ya comenzó o la fecha ya pasó.");
        if (request.Date > BookingWindow.LastAllowedDate(today, options.Value.MaxDaysAhead, options.Value.HorizonUnit))
            return ServiceError.Validation("La fecha excede el plazo máximo de reserva.");
        var user = await context.Users.FindAsync(new object[] { userId }, ct);

        if (user is null)
        {
            return ServiceError.Unauthorized("La sesión no corresponde a un usuario válido.");
        }

        var reservedCount = await context.Reservations.CountAsync(reservation =>
            reservation.ScheduleSlotId == schedule.Id && reservation.Date == request.Date &&
            reservation.Status == ReservationStatus.Reserved, ct);

        if (!ReservationRules.HasAvailableCapacity(reservedCount, schedule.Capacity))
        {
            return ServiceError.Validation("No quedan cupos disponibles para este bloque.");
        }

        var alreadyReserved = await context.Reservations.AnyAsync(reservation =>
            reservation.UserId == userId &&
            reservation.ScheduleSlotId == schedule.Id && reservation.Date == request.Date &&
            reservation.Status == ReservationStatus.Reserved, ct);

        if (alreadyReserved)
        {
            return ServiceError.Validation("Ya tienes una reserva para este bloque.");
        }

        var reservation = new Reservation
        {
            UserId = user.Id,
            User = user,
            ScheduleSlotId = schedule.Id,
            ScheduleSlot = schedule,
            Date = request.Date,
            Status = ReservationStatus.Reserved,
            CreatedAt = clock.UtcNow.UtcDateTime,
        };

        context.Reservations.Add(reservation);
        await context.SaveChangesAsync(ct);

        return ServiceResult<ReservationMessageResponse>.Success(new ReservationMessageResponse("Reserva creada correctamente.", ReservationResponse.From(reservation)));
    }

    public async Task<ServiceResult<ReservationMessageResponse>> CancelAsync(int id, int? currentUserId, bool isAdmin, CancellationToken ct = default)
    {
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

        return ServiceResult<ReservationMessageResponse>.Success(new ReservationMessageResponse("Reserva cancelada correctamente.", ReservationResponse.From(reservation)));
    }
}

