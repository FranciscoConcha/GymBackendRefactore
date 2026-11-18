using System.Globalization;
using GymBackend.Application.Common;
using GymBackend.Data;
using GymBackend.Domain;
using GymBackend.DTOs;
using GymBackend.Time;
using Microsoft.EntityFrameworkCore;

namespace GymBackend.Application.Schedules;

public sealed class ScheduleService(AppDbContext context, IGymClock clock) : IScheduleService
{
    public async Task<ServiceResult<ScheduleResponse[]>> GetEnabledAsync(string? date, CancellationToken ct = default)
    {
        var requestedDate = clock.Today;
        if (date is not null && !DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out requestedDate))
            return ServiceError.Validation("Fecha inválida. Use el formato yyyy-MM-dd.");

        var schedules = await context.ScheduleSlots.Where(slot => slot.Enabled && slot.Day == requestedDate.DayOfWeek)
            .OrderBy(slot => slot.StartTime).ToListAsync(ct);
        var counts = await context.Reservations
            .Where(r => r.Status == ReservationStatus.Reserved && r.Date == requestedDate)
            .GroupBy(r => r.ScheduleSlotId)
            .Select(group => new { Id = group.Key, Count = group.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        return ServiceResult<ScheduleResponse[]>.Success(schedules
            .Select(slot => ScheduleResponse.From(slot, counts.GetValueOrDefault(slot.Id), requestedDate)).ToArray());
    }
}
