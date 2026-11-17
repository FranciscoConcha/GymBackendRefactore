namespace GymBackend.IntegrationTests.Infrastructure;

internal static class GymTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");
    internal static DateTimeOffset Now() => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone);
    internal static DateTime Today() => Now().Date;
}
