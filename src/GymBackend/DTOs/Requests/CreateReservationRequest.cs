using GymBackend.Domain;

namespace GymBackend.DTOs;

public class CreateReservationRequest
{
    public string Day { get; set; } = string.Empty;

    public string StartTime { get; set; } = string.Empty;

    public string EndTime { get; set; } = string.Empty;

    public string Zone { get; set; } = Zones.Default;
}
