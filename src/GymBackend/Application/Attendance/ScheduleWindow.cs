using System.Globalization;

namespace GymBackend.Application.Attendance;

public static class ScheduleWindow
{
    public static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(15);

    public static bool IsWithin(string startTime, string endTime, TimeSpan timeOfDay)
    {
        if (!TimeSpan.TryParseExact(startTime, "hh\\:mm", CultureInfo.InvariantCulture, out var start) ||
            !TimeSpan.TryParseExact(endTime, "hh\\:mm", CultureInfo.InvariantCulture, out var end))
        {
            return false;
        }

        var windowStart = start - Tolerance;
        var windowEnd = end + Tolerance;
        var currentTime = timeOfDay;

        return currentTime >= windowStart && currentTime <= windowEnd;
    }

}
