namespace GymBackend.Models;

public sealed class GroupBooking
{
    public int Id { get; set; }
    public int SportBranchId { get; set; }
    public SportBranch SportBranch { get; set; } = null!;
    public int ScheduleSlotId { get; set; }
    public ScheduleSlot ScheduleSlot { get; set; } = null!;
    public DateOnly Date { get; set; }
    public string Mode { get; set; } = string.Empty;
    public int Seats { get; set; }
    public string Status { get; set; } = "Activa";
    public int CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
}
