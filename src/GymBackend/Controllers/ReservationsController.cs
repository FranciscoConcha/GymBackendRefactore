using GymBackend.DTOs;
using GymBackend.Domain;
using GymBackend.Data;
using GymBackend.Time;
using GymBackend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GymBackend.Extensions;

namespace GymBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReservationsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IGymClock _clock;

    public ReservationsController(AppDbContext context, IGymClock clock)
    {
        _context = context;
        _clock = clock;
    }

    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ReservationResponse[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReservations()
    {
        var reservations = await _context.Reservations
            .OrderByDescending(reservation => reservation.CreatedAt)
            .ToListAsync();

        return Ok(reservations.Select(ReservationResponse.From));
    }

    [HttpGet("user/{userId}")]
    [ProducesResponseType<ReservationResponse[]>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReservationsByUser(int userId)
    {
        if (!User.IsInRole(Roles.Admin))
        {
            var currentUserId = User.GetUserId();
            if (currentUserId is null)
            {
                return Unauthorized(new MessageResponse("La sesión no contiene un usuario válido."));
            }

            if (currentUserId.Value != userId)
            {
                return Forbid();
            }
        }

        var reservations = await _context.Reservations
            .Where(reservation => reservation.UserId == userId)
            .OrderByDescending(reservation => reservation.CreatedAt)
            .ToListAsync();

        return Ok(reservations.Select(ReservationResponse.From));
    }

    [HttpPost]
    [ProducesResponseType<ReservationMessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateReservation(CreateReservationRequest request)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized(new MessageResponse("La sesión no contiene un usuario válido."));
        }
        var day = request.Day.Trim();
        var startTime = request.StartTime.Trim();
        var endTime = request.EndTime.Trim();
        var zone = request.Zone.Trim();

        var schedule = await _context.ScheduleSlots.FirstOrDefaultAsync(slot =>
            slot.Day == day &&
            slot.StartTime == startTime &&
            slot.EndTime == endTime &&
            slot.Zone == zone &&
            slot.Enabled);

        if (schedule is null)
        {
            return BadRequest(new MessageResponse("El bloque seleccionado no está disponible."));
        }

        var user = await _context.Users.FindAsync(userId.Value);

        if (user is null)
        {
            return Unauthorized(new MessageResponse("La sesión no corresponde a un usuario válido."));
        }

        var reservedCount = await _context.Reservations.CountAsync(reservation =>
            reservation.Day == day &&
            reservation.StartTime == startTime &&
            reservation.EndTime == endTime &&
            reservation.Zone == zone &&
            reservation.Status == ReservationStatuses.Reserved);

        if (reservedCount >= schedule.Capacity)
        {
            return BadRequest(new MessageResponse("No quedan cupos disponibles para este bloque."));
        }

        var alreadyReserved = await _context.Reservations.AnyAsync(reservation =>
            reservation.UserId == userId &&
            reservation.Day == day &&
            reservation.StartTime == startTime &&
            reservation.EndTime == endTime &&
            reservation.Zone == zone &&
            reservation.Status == ReservationStatuses.Reserved);

        if (alreadyReserved)
        {
            return BadRequest(new MessageResponse("Ya tienes una reserva para este bloque."));
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
            CreatedAt = _clock.UtcNow.UtcDateTime,
        };

        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        return Ok(new ReservationMessageResponse("Reserva creada correctamente.", ReservationResponse.From(reservation)));
    }

    [HttpDelete("{id}")]
    [ProducesResponseType<ReservationMessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelReservation(int id)
    {
        var reservation = await _context.Reservations.FindAsync(id);

        if (reservation is null)
        {
            return NotFound(new MessageResponse("Reserva no encontrada."));
        }

        if (!User.IsInRole(Roles.Admin))
        {
            var currentUserId = User.GetUserId();
            if (currentUserId is null)
            {
                return Unauthorized(new MessageResponse("La sesión no contiene un usuario válido."));
            }

            if (reservation.UserId != currentUserId.Value)
            {
                return Forbid();
            }
        }

        reservation.Status = ReservationStatuses.Cancelled;
        await _context.SaveChangesAsync();

        return Ok(new ReservationMessageResponse("Reserva cancelada correctamente.", ReservationResponse.From(reservation)));
    }
}
