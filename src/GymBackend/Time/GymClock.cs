using GymBackend.Options;
using Microsoft.Extensions.Options;

namespace GymBackend.Time;

public sealed class GymClock(TimeProvider timeProvider, IOptions<GymOptions> options) : IGymClock
{
    public TimeZoneInfo TimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
    public DateTimeOffset LocalNow => TimeZoneInfo.ConvertTime(UtcNow, TimeZone);
    public DateOnly Today => DateOnly.FromDateTime(LocalNow.DateTime);

    public DateTimeOffset StartOfDay(DateOnly date)
    {
        var midnight = date.ToDateTime(TimeOnly.MinValue);
        // Santiago skips midnight when daylight saving starts: the day begins at 01:00.
        while (TimeZone.IsInvalidTime(midnight))
            midnight = midnight.AddMinutes(1);
        return new DateTimeOffset(midnight, TimeZone.GetUtcOffset(midnight));
    }
}
