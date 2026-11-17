using GymBackend.Application.Common;
using GymBackend.DTOs;
using GymBackend.Data;
using GymBackend.Domain;
using GymBackend.Models;
using GymBackend.Time;
using Microsoft.EntityFrameworkCore;

namespace GymBackend.Application.Reservations;

public sealed class ReservationService(AppDbContext context, IGymClock clock) : IReservationService
{
    public async Task<ServiceResult<ReservationResponse[]>> GetAllAsync(CancellationToken ct = default)
    {
        var reservations = await context.Reservations
            .OrderByDescending(reservation => reservation.CreatedAt)
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

        var reservations = await context.Reservations
            .Where(reservation => reservation.UserId == requestedUserId)
            .OrderByDescending(reservation => reservation.CreatedAt)
            .ToListAsync(ct);

        return ServiceResult<ReservationResponse[]>.Success(reservations.Select(ReservationResponse.From).ToArray());
    }

    public async Task<ServiceResult<ReservationMessageResponse>> CreateAsync(int userId, CreateReservationRequest request, CancellationToken ct = default)
    {
        var day = request.Day.Trim();
        var startTime = request.StartTime.Trim();
        var endTime = request.EndTime.Trim();
        var zone = request.Zone.Trim();

        var schedule = await context.ScheduleSlots.FirstOrDefaultAsync(slot =>
            slot.Day == day &&
            slot.StartTime == startTime &&
            slot.EndTime == endTime &&
            slot.Zone == zone &&
            slot.Enabled, ct);

        if (schedule is null)
        {
            return ServiceError.Validation("El bloque seleccionado no está disponible.");
        }

        var user = await context.Users.FindAsync(new object[] { userId }, ct);

        if (user is null)
        {
            return ServiceError.Unauthorized("La sesión no corresponde a un usuario válido.");
        }

        var reservedCount = await context.Reservations.CountAsync(reservation =>
            reservation.Day == day &&
            reservation.StartTime == startTime &&
            reservation.EndTime == endTime &&
            reservation.Zone == zone &&
            reservation.Status == ReservationStatuses.Reserved, ct);

        if (!ReservationRules.HasAvailableCapacity(reservedCount, schedule.Capacity))
        {
            return ServiceError.Validation("No quedan cupos disponibles para este bloque.");
        }

        var alreadyReserved = await context.Reservations.AnyAsync(reservation =>
            reservation.UserId == userId &&
            reservation.Day == day &&
            reservation.StartTime == startTime &&
            reservation.EndTime == endTime &&
            reservation.Zone == zone &&
            reservation.Status == ReservationStatuses.Reserved, ct);

        if (alreadyReserved)
        {
            return ServiceError.Validation("Ya tienes una reserva para este bloque.");
        }

        var reservation = new Reservation
        {
            UserId = user.Id,
            UserName = user.Name,
            Day = day,
            StartTime = startTime,
            EndTime = endTime,
            Zone = zone,
            Capacity = schedule.Capacity,
            Status = ReservationStatuses.Reserved,
            CreatedAt = clock.UtcNow.UtcDateTime,
        };

        context.Reservations.Add(reservation);
        await context.SaveChangesAsync(ct);

        return ServiceResult<ReservationMessageResponse>.Success(new ReservationMessageResponse("Reserva creada correctamente.", ReservationResponse.From(reservation)));
    }

    public async Task<ServiceResult<ReservationMessageResponse>> CancelAsync(int id, int? currentUserId, bool isAdmin, CancellationToken ct = default)
    {
        var reservation = await context.Reservations.FindAsync(new object[] { id }, ct);

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

        reservation.Status = ReservationStatuses.Cancelled;
        await context.SaveChangesAsync(ct);

        return ServiceResult<ReservationMessageResponse>.Success(new ReservationMessageResponse("Reserva cancelada correctamente.", ReservationResponse.From(reservation)));
    }
}
