using GymBackend.Application.Reservations;

namespace GymBackend.UnitTests.Reservations;

public sealed class ReservationRulesTests
{
    [Theory]
    [InlineData("Estudiante", 0, true)]
    [InlineData("Estudiante", 1, false)]
    [InlineData("Estudiante", 2, false)]
    [InlineData("Deportista", 2, true)]
    [InlineData("Funcionario", 2, true)]
    [InlineData("Externo", 2, true)]
    [InlineData(null, 2, true)]
    public void CanBookOnDate_OnlyStudentsHaveDailyLimit(string? category, int activeBookings, bool expected)
    {
        Assert.Equal(expected, ReservationRules.CanBookOnDate(category, activeBookings));
    }

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
