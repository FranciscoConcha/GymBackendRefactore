using System.Globalization;
using GymBackend.Domain;
using GymBackend.Models;

namespace GymBackend.DTOs;

public sealed record ReservationResponse(int Id, int UserId, string UserName, int ScheduleSlotId, string Date, string Day, string StartTime, string EndTime, string Zone, int Capacity, string Status, DateTime CreatedAt)
{
    public static ReservationResponse From(Reservation entity) =>
        new(entity.Id, entity.UserId, entity.User.FullName, entity.ScheduleSlotId,
            entity.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), GymWeekdays.Name(entity.ScheduleSlot.Day),
            entity.ScheduleSlot.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            entity.ScheduleSlot.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            entity.ScheduleSlot.Zone, entity.ScheduleSlot.Capacity, ReservationStatuses.Name(entity.Status), entity.CreatedAt);
}
