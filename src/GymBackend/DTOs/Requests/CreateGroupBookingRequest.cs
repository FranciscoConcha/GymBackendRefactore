namespace GymBackend.DTOs;

public sealed class CreateGroupBookingRequest
{
    public int SportBranchId { get; set; }
    public int ScheduleSlotId { get; set; }
    public DateOnly Date { get; set; }
    public string? Mode { get; set; }
    public int? Seats { get; set; }
}
