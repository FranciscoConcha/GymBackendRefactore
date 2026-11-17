using GymBackend.Application.Common;
using GymBackend.Data;
using GymBackend.Domain;
using GymBackend.DTOs;
using Microsoft.EntityFrameworkCore;

namespace GymBackend.Application.Schedules;

public sealed class ScheduleService(AppDbContext context) : IScheduleService
{
    public async Task<ServiceResult<ScheduleResponse[]>> GetEnabledAsync(CancellationToken ct = default)
    {
        var schedules = await context.ScheduleSlots
            .Where(slot => slot.Enabled)
            .OrderBy(slot => slot.Id)
            .ToListAsync(ct);

        var counts = await context.Reservations
            .Where(reservation => reservation.Status == ReservationStatuses.Reserved)
            .GroupBy(reservation => new { reservation.Day, reservation.StartTime, reservation.EndTime, reservation.Zone })
            .Select(group => new
            {
                group.Key.Day,
                group.Key.StartTime,
                group.Key.EndTime,
                group.Key.Zone,
                Count = group.Count(),
            })
            .ToListAsync(ct);

        var countsByBlock = counts.ToDictionary(
            count => (count.Day, count.StartTime, count.EndTime, count.Zone),
            count => count.Count);
        var result = schedules.Select(schedule => ScheduleResponse.From(schedule,
            countsByBlock.GetValueOrDefault((schedule.Day, schedule.StartTime, schedule.EndTime, schedule.Zone))))
            .ToArray();

        return ServiceResult<ScheduleResponse[]>.Success(result);
    }
}
