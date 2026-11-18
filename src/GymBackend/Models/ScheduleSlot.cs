using GymBackend.Domain;
namespace GymBackend.Models;

public class ScheduleSlot
{
    public int Id { get; set; }

    public DayOfWeek Day { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string Zone { get; set; } = Zones.Default;

    public int Capacity { get; set; } = 20;

    public bool Enabled { get; set; } = true;
}

