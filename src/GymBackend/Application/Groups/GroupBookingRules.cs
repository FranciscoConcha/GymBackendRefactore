namespace GymBackend.Application.Groups;

public static class GroupBookingRules
{
    public static bool HasLeadTime(DateTimeOffset now, DateOnly date, TimeOnly start, TimeZoneInfo zone)
    {
        return ToInstant(date, start, zone) is { } instant && instant - now >= TimeSpan.FromMinutes(60);
    }

    public static DateTimeOffset? ToInstant(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(time);
        if (zone.IsInvalidTime(local)) return null;
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    public static int? ResolveSeats(string? mode, int? seats, int available) => mode switch
    {
        "Cupos" when seats >= 1 && seats <= available => seats,
        "TotalDisponible" when available > 0 => available,
        _ => null,
    };
}
