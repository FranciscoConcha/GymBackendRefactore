namespace GymBackend.Models;

public class Attendance
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;



    public DateOnly Date { get; set; }

    public DateTime? EntryTime { get; set; }

    public string? EntryAdminRut { get; set; }

    public DateTime? ExitTime { get; set; }

    public string? ExitAdminRut { get; set; }

    public int? ReservationId { get; set; }
    public Reservation? Reservation { get; set; }

    public bool CheckedIn { get; set; }
}

