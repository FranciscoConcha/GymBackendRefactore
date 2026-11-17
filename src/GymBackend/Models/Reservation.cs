using GymBackend.Domain;
namespace GymBackend.Models;

public class Reservation
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Day { get; set; } = string.Empty;

    public string StartTime { get; set; } = string.Empty;

    public string EndTime { get; set; } = string.Empty;

    public string Zone { get; set; } = Zones.Default;

    public int Capacity { get; set; } = 20;

    public string Status { get; set; } = ReservationStatuses.Reserved;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}