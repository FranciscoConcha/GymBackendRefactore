using GymBackend.Application.Groups;

namespace GymBackend.UnitTests.Groups;

public sealed class GroupBookingRulesTests
{
    [Theory]
    [InlineData(59, false)]
    [InlineData(60, true)]
    [InlineData(61, true)]
    public void LeadTime_RequiresAtLeastSixtyMinutes(int minutes, bool expected)
    {
        var start = new DateTime(2026, 11, 17, 10, 0, 0);
        Assert.Equal(expected, GroupBookingRules.HasLeadTime(new DateTimeOffset(start.AddMinutes(-minutes), TimeSpan.Zero), DateOnly.FromDateTime(start), TimeOnly.FromDateTime(start), TimeZoneInfo.Utc));
    }

    [Theory]
    [InlineData("Cupos", 0, 10, null)]
    [InlineData("Cupos", null, 10, null)]
    [InlineData("Cupos", 3, 2, null)]
    [InlineData("Cupos", 2, 2, 2)]
    [InlineData("TotalDisponible", null, 10, 10)]
    [InlineData("TotalDisponible", 1, 10, 10)]
    [InlineData("TotalDisponible", null, 0, null)]
    [InlineData("otro", 1, 10, null)]
    public void Modes_AreAllOrNothing(string mode, int? requested, int available, int? expected) =>
        Assert.Equal(expected, GroupBookingRules.ResolveSeats(mode, requested, available));
}
