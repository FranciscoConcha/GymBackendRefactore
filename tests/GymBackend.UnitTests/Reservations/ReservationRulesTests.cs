using GymBackend.Application.Reservations;

namespace GymBackend.UnitTests.Reservations;

public sealed class ReservationRulesTests
{
    [Theory]
    [InlineData(20, 20, false)]
    [InlineData(21, 20, false)]
    [InlineData(19, 20, true)]
    [InlineData(0, 20, true)]
    [InlineData(0, 0, false)]
    public void HasAvailableCapacity_ComparesReservedCountWithCapacity(int reservedCount, int capacity, bool expected)
    {
        Assert.Equal(expected, ReservationRules.HasAvailableCapacity(reservedCount, capacity));
    }
}
