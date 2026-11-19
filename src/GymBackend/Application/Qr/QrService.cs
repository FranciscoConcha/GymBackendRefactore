using GymBackend.Application.Common;
using GymBackend.Data;
using GymBackend.Domain;
using GymBackend.DTOs;
using GymBackend.Time;
using Microsoft.EntityFrameworkCore;

namespace GymBackend.Application.Qr;

public sealed class QrService(AppDbContext context, IQrTokenService tokens, IGymClock clock) : IQrService
{
    private readonly AppDbContext _context = context;
    private readonly IGymClock _clock = clock;

    public async Task<ServiceResult<QrCodeResponse>> GetMyCodeAsync(int userId, CancellationToken ct = default)
    {
        var user = await _context.Users.Include(u => u.SportBranch).SingleOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
        {
            return ServiceError.Unauthorized("La sesión no corresponde a un usuario válido.");
        }

        var today = _clock.Today;
        var token = tokens.Generate(user.Rut, today);
        var time = TimeOnly.FromDateTime(_clock.LocalNow.DateTime);
        var reservations = await _context.Reservations.Include(r => r.ScheduleSlot).Where(r => r.UserId == userId
            && r.Date == today && (r.Status == ReservationStatus.Reserved || r.Status == ReservationStatus.Attended)
            && r.ScheduleSlot.Enabled && r.ScheduleSlot.EndTime > time).ToArrayAsync(ct);
        var sessions = reservations.Select(AccessSessionResponse.From).ToList();
        if (user.Category == UserCategories.Deportista && user.SportBranchId is not null)
        {
            var groups = await _context.GroupBookings.Include(g => g.ScheduleSlot).Where(g => g.Date == today && g.Status == "Activa"
                && g.SportBranchId == user.SportBranchId && g.SportBranch.Enabled && g.ScheduleSlot.Enabled
                && g.ScheduleSlot.EndTime > time).ToArrayAsync(ct);
            sessions.AddRange(groups.Select(AccessSessionResponse.From));
        }
        var nextSession = sessions.OrderBy(s => s.StartTime, StringComparer.Ordinal).ThenBy(s => s.Kind, StringComparer.Ordinal).FirstOrDefault();

        return ServiceResult<QrCodeResponse>.Success(new QrCodeResponse(user.Rut, user.FullName, today.ToString("yyyy-MM-dd"), token,
            _clock.StartOfDay(today), _clock.StartOfDay(today.AddDays(1)).AddMinutes(-1), user.FirstName, user.LastName,
            user.Category, user.SportBranch is { } branch ? new(branch.Id, branch.Name) : null, nextSession));
    }
}
