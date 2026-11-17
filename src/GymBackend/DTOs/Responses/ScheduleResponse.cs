using GymBackend.Models;

namespace GymBackend.DTOs;

public sealed record ScheduleResponse(int Id, string Day, string StartTime, string EndTime, string Zone, int Capacity, int ReservedCount)
{
    public static ScheduleResponse From(ScheduleSlot entity, int reservedCount) =>
        new(entity.Id, entity.Day, entity.StartTime, entity.EndTime, entity.Zone, entity.Capacity, reservedCount);
}
