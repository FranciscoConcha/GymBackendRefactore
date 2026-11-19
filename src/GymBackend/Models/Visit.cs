namespace GymBackend.Models;

public sealed class Visit
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int? ReservationId { get; set; }
    public Reservation? Reservation { get; set; }
    public int? GroupBookingId { get; set; }
    public GroupBooking? GroupBooking { get; set; }
    public DateOnly Date { get; set; }
    public DateTime EntryAt { get; set; }
    public DateTime? ExitAt { get; set; }
    public int EntryOperatorId { get; set; }
    public User EntryOperator { get; set; } = null!;
    public int? ExitOperatorId { get; set; }
    public User? ExitOperator { get; set; }
    public string DocumentType { get; set; } = string.Empty;
}
