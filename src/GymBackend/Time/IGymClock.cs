namespace GymBackend.Time;

public interface IGymClock
{
    DateTimeOffset UtcNow { get; }
    DateTimeOffset LocalNow { get; }
    DateOnly Today { get; }
    TimeZoneInfo TimeZone { get; }
    DateTimeOffset StartOfDay(DateOnly date);
}
