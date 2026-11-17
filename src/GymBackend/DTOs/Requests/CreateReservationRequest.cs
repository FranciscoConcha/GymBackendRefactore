namespace GymBackend.DTOs;

public class CreateReservationRequest
{
    public int ScheduleSlotId { get; set; }
    public DateOnly Date { get; set; }
}
