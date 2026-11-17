using GymBackend.DTOs;
using GymBackend.Domain;
using GymBackend.Data;
using GymBackend.Time;
using GymBackend.Options;
using Microsoft.Extensions.Options;
using GymBackend.Models;
using GymBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using GymBackend.Extensions;

namespace GymBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Admin)]
public class AttendanceController : ControllerBase
{
    private static readonly TimeSpan ScheduleTolerance = TimeSpan.FromMinutes(15);

    private readonly AppDbContext _context;
    private readonly IGymClock _clock;
    private readonly IOptions<JwtOptions> _jwtOptions;

    public AttendanceController(AppDbContext context, IOptions<JwtOptions> jwtOptions, IGymClock clock)
    {
        _context = context;
        _clock = clock;
        _jwtOptions = jwtOptions;
    }

    [HttpPost("entry")]
    [ProducesResponseType<AttendanceMessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RegisterEntry(AttendanceActionRequest request)
    {
        var now = _clock.LocalNow;

        var (user, errorMessage) = await ResolveUserFromTokenAsync(request.Token, now.DateTime);
        if (user is null)
        {
            return BadRequest(new MessageResponse(errorMessage));
        }

        if (!user!.Enabled)
        {
            return BadRequest(new MessageResponse("El usuario no está habilitado."));
        }

        var todayKey = now.DateTime.Date.ToString("yyyy-MM-dd");

        var attendance = await _context.Attendances.FirstOrDefaultAsync(candidate =>
            candidate.UserId == user.Id && candidate.Date == todayKey);

        if (attendance is not null && attendance.CheckedIn)
        {
            return BadRequest(new MessageResponse("Usuario ya registra una entrada en el día."));
        }

        Reservation? matchedReservation;

        if (attendance is null)
        {
            var todayName = GymWeekdays.Name(now.DateTime.DayOfWeek);

            var todaysReservations = await _context.Reservations
                .Where(reservation =>
                    reservation.UserId == user.Id &&
                    reservation.Day == todayName &&
                    reservation.Status == ReservationStatuses.Reserved)
                .ToListAsync();

            matchedReservation = todaysReservations.FirstOrDefault(reservation => IsWithinSchedule(reservation, now.DateTime));
        }
        else
        {
            matchedReservation = attendance.ReservationId.HasValue
                ? await _context.Reservations.FindAsync(attendance.ReservationId.Value)
                : null;

            if (matchedReservation is not null && !IsWithinSchedule(matchedReservation, now.DateTime))
            {
                matchedReservation = null;
            }
        }

        if (matchedReservation is null)
        {
            return BadRequest(new MessageResponse("El usuario no se encuentra registrado en el horario de ingreso."));
        }

        var adminRut = CurrentAdminRut();

        if (attendance is null)
        {
            attendance = new Attendance
            {
                UserId = user.Id,
                Rut = user.Rut,
                UserName = user.Name,
                Date = todayKey,
                EntryTime = _clock.UtcNow.UtcDateTime,
                EntryAdminRut = adminRut,
                CheckedIn = true,
                ReservationId = matchedReservation.Id,
            };

            _context.Attendances.Add(attendance);
            matchedReservation.Status = ReservationStatuses.Attended;
        }
        else
        {
            attendance.CheckedIn = true;
        }

        await _context.SaveChangesAsync();

        return Ok(new AttendanceMessageResponse("Entrada registrada correctamente.", AttendanceResponse.From(attendance)));
    }

    [HttpPost("exit")]
    [ProducesResponseType<AttendanceMessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RegisterExit(AttendanceActionRequest request)
    {
        var now = _clock.LocalNow;

        var (user, errorMessage) = await ResolveUserFromTokenAsync(request.Token, now.DateTime);
        if (user is null)
        {
            return BadRequest(new MessageResponse(errorMessage));
        }

        if (!user!.Enabled)
        {
            return BadRequest(new MessageResponse("El usuario no está habilitado."));
        }

        var todayKey = now.DateTime.Date.ToString("yyyy-MM-dd");

        var attendance = await _context.Attendances.FirstOrDefaultAsync(candidate =>
            candidate.UserId == user.Id && candidate.Date == todayKey);

        if (attendance is null || !attendance.CheckedIn)
        {
            return BadRequest(new MessageResponse("Usuario no registra una entrada en el día."));
        }

        attendance.ExitTime = _clock.UtcNow.UtcDateTime;
        attendance.ExitAdminRut = CurrentAdminRut();
        attendance.CheckedIn = false;

        await _context.SaveChangesAsync();

        return Ok(new AttendanceMessageResponse("Salida registrada correctamente.", AttendanceResponse.From(attendance)));
    }

    private async Task<(User? User, string Error)> ResolveUserFromTokenAsync(string? token, DateTime now)
    {
        var signingKey = _jwtOptions.Value.Key
            ?? throw new InvalidOperationException("Jwt:Key no está configurada.");

        if (!QrTokenService.TryValidateToken(token, signingKey, now.Date, out var rut, out var errorMessage))
        {
            return (null, errorMessage);
        }

        var user = await _context.Users.FirstOrDefaultAsync(candidate => candidate.Rut == rut);

        if (user is null)
        {
            errorMessage = "Código QR inválido.";
            return (null, errorMessage);
        }

        return (user, string.Empty);
    }

    private static bool IsWithinSchedule(Reservation reservation, DateTime now)
    {
        if (!TimeSpan.TryParseExact(reservation.StartTime, "hh\\:mm", CultureInfo.InvariantCulture, out var start) ||
            !TimeSpan.TryParseExact(reservation.EndTime, "hh\\:mm", CultureInfo.InvariantCulture, out var end))
        {
            return false;
        }

        var windowStart = start - ScheduleTolerance;
        var windowEnd = end + ScheduleTolerance;
        var currentTime = now.TimeOfDay;

        return currentTime >= windowStart && currentTime <= windowEnd;
    }

    private string CurrentAdminRut()
    {
        return User.GetRut() ?? string.Empty;
    }
}
