using System.Globalization;
using GymBackend.Domain;
using GymBackend.Models;

namespace GymBackend.DTOs;

public sealed record ScheduleResponse(int Id, string Day, string StartTime, string EndTime, string Zone, int Capacity, int ReservedCount, string Date)
{
    public static ScheduleResponse From(ScheduleSlot entity, int reservedCount, DateOnly date) =>
        new(entity.Id, GymWeekdays.Name(entity.Day), entity.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            entity.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture), entity.Zone, entity.Capacity, reservedCount,
            date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
}
