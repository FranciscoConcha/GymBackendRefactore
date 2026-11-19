using System.Globalization;
using GymBackend.Models;

namespace GymBackend.DTOs;

public sealed record GroupBookingMessageResponse(string Message, GroupBookingResponse GroupBooking);
public sealed record GroupBookingResponse(int Id, UserBranchResponse SportBranch, int ScheduleSlotId, string Date,
    string StartTime, string EndTime, string Mode, int Seats, int UsedSeats, string Status, string CreatedBy, DateTimeOffset CreatedAt)
{
    public static GroupBookingResponse From(GroupBooking group, int usedSeats) => new(group.Id,
        new(group.SportBranchId, group.SportBranch.Name), group.ScheduleSlotId,
        group.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), group.ScheduleSlot.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
        group.ScheduleSlot.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture), group.Mode, group.Seats, usedSeats,
        group.Status, group.CreatedBy.FullName, group.CreatedAt);
}
