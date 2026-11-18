using GymBackend.Application.Common;
using GymBackend.Application.Qr;
using GymBackend.Data;
using GymBackend.Domain;
using GymBackend.DTOs;
using GymBackend.Models;
using GymBackend.Time;
using Microsoft.EntityFrameworkCore;

namespace GymBackend.Application.Attendance;

public sealed class AttendanceService(AppDbContext context, IQrTokenService tokens, IGymClock clock) : IAttendanceService
{
    private readonly AppDbContext _context = context;
    private readonly IQrTokenService _tokens = tokens;
    private readonly IGymClock _clock = clock;

    public async Task<ServiceResult<AttendanceMessageResponse>> RegisterEntryAsync(string token, string adminRut, CancellationToken ct = default)
    {
        var now = _clock.LocalNow;

        var (user, errorMessage) = await ResolveUserFromTokenAsync(token, now.DateTime, ct);
        if (user is null)
        {
            return ServiceError.Validation(errorMessage);
        }

        if (!user!.Enabled)
        {
            return ServiceError.Validation("El usuario no está habilitado.");
        }

        var todayKey = _clock.Today;

        var attendance = await _context.Attendances.FirstOrDefaultAsync(candidate =>
            candidate.UserId == user.Id && candidate.Date == todayKey, ct);

        if (attendance is not null && attendance.CheckedIn)
        {
            return ServiceError.Validation("Usuario ya registra una entrada en el día.");
        }

        Reservation? matchedReservation;

        if (attendance is null)
        {


            var todaysReservations = await _context.Reservations.Include(r => r.ScheduleSlot)
                .Where(reservation =>
                    reservation.UserId == user.Id &&
                    reservation.Date == todayKey &&
                    reservation.Status == ReservationStatus.Reserved)
                .ToListAsync(ct);

            matchedReservation = todaysReservations.FirstOrDefault(reservation => ScheduleWindow.IsWithin(reservation.ScheduleSlot.StartTime, reservation.ScheduleSlot.EndTime, TimeOnly.FromDateTime(now.DateTime)));
        }
        else
        {
            matchedReservation = attendance.ReservationId.HasValue
                ? await _context.Reservations.Include(r => r.ScheduleSlot).FirstOrDefaultAsync(r => r.Id == attendance.ReservationId.Value && r.Date == todayKey, ct)
                : null;

            if (matchedReservation is not null && !ScheduleWindow.IsWithin(matchedReservation.ScheduleSlot.StartTime, matchedReservation.ScheduleSlot.EndTime, TimeOnly.FromDateTime(now.DateTime)))
            {
                matchedReservation = null;
            }
        }

        if (matchedReservation is null)
        {
            return ServiceError.Validation("El usuario no se encuentra registrado en el horario de ingreso.");
        }

        if (attendance is null)
        {
            attendance = new GymBackend.Models.Attendance
            {
                UserId = user.Id,
                User = user,
                Date = todayKey,
                EntryTime = _clock.UtcNow.UtcDateTime,
                EntryAdminRut = adminRut,
                CheckedIn = true,
                ReservationId = matchedReservation.Id,
            };

            _context.Attendances.Add(attendance);
            matchedReservation.Status = ReservationStatus.Attended;
        }
        else
        {
            attendance.CheckedIn = true;
        }

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            return ServiceError.Conflict("Usuario ya registra una entrada en el día.");
        }

        return ServiceResult<AttendanceMessageResponse>.Success(new AttendanceMessageResponse("Entrada registrada correctamente.", AttendanceResponse.From(attendance)));
    }

    public async Task<ServiceResult<AttendanceMessageResponse>> RegisterExitAsync(string token, string adminRut, CancellationToken ct = default)
    {
        var now = _clock.LocalNow;

        var (user, errorMessage) = await ResolveUserFromTokenAsync(token, now.DateTime, ct);
        if (user is null)
        {
            return ServiceError.Validation(errorMessage);
        }

        if (!user!.Enabled)
        {
            return ServiceError.Validation("El usuario no está habilitado.");
        }

        var todayKey = _clock.Today;

        var attendance = await _context.Attendances.FirstOrDefaultAsync(candidate =>
            candidate.UserId == user.Id && candidate.Date == todayKey, ct);

        if (attendance is null || !attendance.CheckedIn)
        {
            return ServiceError.Validation("Usuario no registra una entrada en el día.");
        }

        attendance.ExitTime = _clock.UtcNow.UtcDateTime;
        attendance.ExitAdminRut = adminRut;
        attendance.CheckedIn = false;

        await _context.SaveChangesAsync(ct);

        return ServiceResult<AttendanceMessageResponse>.Success(new AttendanceMessageResponse("Salida registrada correctamente.", AttendanceResponse.From(attendance)));
    }

    private async Task<(User? User, string Error)> ResolveUserFromTokenAsync(string? token, DateTime now, CancellationToken ct)
    {
        if (!_tokens.TryValidate(token, DateOnly.FromDateTime(now), out var rut, out var errorMessage))
        {
            return (null, errorMessage);
        }

        var user = await _context.Users.FirstOrDefaultAsync(candidate => candidate.Rut == rut, ct);

        if (user is null)
        {
            errorMessage = "Código QR inválido.";
            return (null, errorMessage);
        }

        return (user, string.Empty);
    }

}

