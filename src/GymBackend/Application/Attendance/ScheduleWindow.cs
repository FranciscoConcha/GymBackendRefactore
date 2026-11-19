namespace GymBackend.Application.Attendance;

public static class ScheduleWindow
{
    public static readonly TimeSpan EarlyEntry = TimeSpan.FromMinutes(15);

    // Ventana de ingreso: [inicio − 15 min, fin). Sin tolerancia tras el fin; la salida no depende de ella.
    public static bool IsWithin(TimeOnly startTime, TimeOnly endTime, TimeOnly timeOfDay) =>
        timeOfDay.ToTimeSpan() >= startTime.ToTimeSpan() - EarlyEntry &&
        timeOfDay < endTime;
}
