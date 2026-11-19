namespace GymBackend.Models;

public sealed class AttendanceAlert
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
}
