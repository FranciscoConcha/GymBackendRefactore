namespace GymBackend.Application.Attendance;

public static class ScheduleWindow
{
    public static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(15);

    public static bool IsWithin(TimeOnly startTime, TimeOnly endTime, TimeOnly timeOfDay) =>
        timeOfDay.ToTimeSpan() >= startTime.ToTimeSpan() - Tolerance &&
        timeOfDay.ToTimeSpan() <= endTime.ToTimeSpan() + Tolerance;
}
