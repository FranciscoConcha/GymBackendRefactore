using GymBackend.Application.Attendance;

namespace GymBackend.UnitTests.Attendance;

public sealed class ScheduleWindowTests
{
    [Theory]
    [InlineData(9, 45, true)]
    [InlineData(9, 44, false)]
    [InlineData(11, 15, true)]
    [InlineData(11, 16, false)]
    [InlineData(10, 30, true)]
    public void IsWithin_IncludesExactToleranceBoundaries(int hour, int minute, bool expected)
    {
        Assert.Equal(TimeSpan.FromMinutes(15), ScheduleWindow.Tolerance);
        Assert.Equal(expected, ScheduleWindow.IsWithin(new TimeOnly(10, 0), new TimeOnly(11, 0), new TimeOnly(hour, minute)));
    }

    [Theory]
    [InlineData(9, 44, 59, false)]
    [InlineData(9, 45, 0, true)]
    [InlineData(11, 15, 0, true)]
    [InlineData(11, 15, 1, false)]
    public void IsWithin_RespectsSeconds(int hour, int minute, int second, bool expected)
    {
        Assert.Equal(expected, ScheduleWindow.IsWithin(new TimeOnly(10, 0), new TimeOnly(11, 0), new TimeOnly(hour, minute, second)));
    }

    [Theory]
    [InlineData(0, 0, 0, 30, 0, 0, true)]
    [InlineData(23, 0, 23, 59, 23, 59, true)]
    [InlineData(23, 0, 23, 59, 0, 0, false)]
    public void IsWithin_DoesNotWrapToAnotherDate(int startHour, int startMinute, int endHour, int endMinute, int hour, int minute, bool expected)
    {
        Assert.Equal(expected, ScheduleWindow.IsWithin(new TimeOnly(startHour, startMinute), new TimeOnly(endHour, endMinute), new TimeOnly(hour, minute)));
    }
}
