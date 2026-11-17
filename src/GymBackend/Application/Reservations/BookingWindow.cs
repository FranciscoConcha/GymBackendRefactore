using GymBackend.Options;

namespace GymBackend.Application.Reservations;

public static class BookingWindow
{
    public static DateOnly LastAllowedDate(DateOnly today, int maxDaysAhead, ReservationHorizonUnit unit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxDaysAhead);
        if (unit == ReservationHorizonUnit.CalendarDays) return today.AddDays(maxDaysAhead);
        if (unit != ReservationHorizonUnit.BusinessDays) throw new ArgumentOutOfRangeException(nameof(unit));
        var date = today;
        for (var days = 0; days < maxDaysAhead;)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) days++;
        }
        return date;
    }
}
