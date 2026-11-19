namespace GymBackend.Application.Groups;

public static class GroupBookingRules
{
    public static bool HasLeadTime(DateTimeOffset now, DateOnly date, TimeOnly start, TimeZoneInfo zone)
    {
        var localStart = date.ToDateTime(start);
        // Reject nonexistent local times and use the earliest occurrence of an ambiguous time.
        if (zone.IsInvalidTime(localStart)) return false;
        var offset = zone.IsAmbiguousTime(localStart) ? zone.GetAmbiguousTimeOffsets(localStart).Max() : zone.GetUtcOffset(localStart);
        return new DateTimeOffset(localStart, offset) - now >= TimeSpan.FromMinutes(60);
    }

    public static int? ResolveSeats(string? mode, int? seats, int available) => mode switch
    {
        "Cupos" when seats >= 1 && seats <= available => seats,
        "TotalDisponible" when available > 0 => available,
        _ => null,
    };
}
