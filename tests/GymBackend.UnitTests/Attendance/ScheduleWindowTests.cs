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
        Assert.Equal(expected, ScheduleWindow.IsWithin("10:00", "11:00", new TimeSpan(hour, minute, 0)));
    }

    [Theory]
    [InlineData("invalid", "11:00")]
    [InlineData("10:00", "invalid")]
    [InlineData("10:00:00", "11:00")]
    [InlineData("10:00", "11:00:00")]
    [InlineData("", "11:00")]
    [InlineData("10:00", "")]
    public void IsWithin_InvalidFormat_ReturnsFalse(string start, string end)
    {
        Assert.False(ScheduleWindow.IsWithin(start, end, new TimeSpan(10, 30, 0)));
    }
}
