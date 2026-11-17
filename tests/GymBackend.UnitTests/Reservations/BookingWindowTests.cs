using GymBackend.Application.Reservations;
using GymBackend.Options;

namespace GymBackend.UnitTests.Reservations;

public sealed class BookingWindowTests
{
    [Theory]
    [InlineData("2026-11-20", 1, ReservationHorizonUnit.CalendarDays, "2026-11-21")]
    [InlineData("2026-11-17", 7, ReservationHorizonUnit.CalendarDays, "2026-11-24")]
    [InlineData("2026-12-31", 1, ReservationHorizonUnit.CalendarDays, "2027-01-01")]
    [InlineData("2026-11-20", 1, ReservationHorizonUnit.BusinessDays, "2026-11-23")]
    [InlineData("2026-11-21", 1, ReservationHorizonUnit.BusinessDays, "2026-11-23")]
    [InlineData("2026-11-22", 5, ReservationHorizonUnit.BusinessDays, "2026-11-27")]
    [InlineData("2026-11-17", 7, ReservationHorizonUnit.BusinessDays, "2026-11-26")]
    [InlineData("2026-11-21", 0, ReservationHorizonUnit.CalendarDays, "2026-11-21")]
    [InlineData("2026-11-21", 0, ReservationHorizonUnit.BusinessDays, "2026-11-21")]
    public void LastAllowedDate_CountsRequestedDays(string today, int days, ReservationHorizonUnit unit, string expected)
        => Assert.Equal(DateOnly.ParseExact(expected, "yyyy-MM-dd"),
            BookingWindow.LastAllowedDate(DateOnly.ParseExact(today, "yyyy-MM-dd"), days, unit));

    [Fact]
    public void LastAllowedDate_RejectsNegativeDays()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            BookingWindow.LastAllowedDate(new DateOnly(2026, 11, 17), -1, ReservationHorizonUnit.CalendarDays));

    [Fact]
    public void LastAllowedDate_RejectsUnknownUnit()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            BookingWindow.LastAllowedDate(new DateOnly(2026, 11, 17), 1, (ReservationHorizonUnit)99));
}
