using GymBackend.Domain;
namespace GymBackend.Models;

public class Reservation
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int ScheduleSlotId { get; set; }
    public ScheduleSlot ScheduleSlot { get; set; } = null!;
    public DateOnly Date { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Reserved;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
