using GymBackend.Extensions;
using GymBackend.Options;
using GymBackend.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using System.Globalization;

namespace GymBackend.UnitTests.Time;

public sealed class GymClockTests
{
    [Theory]
    [InlineData("2026-11-18T02:50:00Z", "2026-11-17", 23, 50, -3)]
    [InlineData("2026-11-17T12:00:00Z", "2026-11-17", 9, 0, -3)]
    [InlineData("2026-07-14T12:00:00Z", "2026-07-14", 8, 0, -4)]
    [InlineData("2026-04-04T12:00:00Z", "2026-04-04", 9, 0, -3)]
    [InlineData("2026-04-06T12:00:00Z", "2026-04-06", 8, 0, -4)]
    [InlineData("2026-09-05T12:00:00Z", "2026-09-05", 8, 0, -4)]
    [InlineData("2026-09-07T12:00:00Z", "2026-09-07", 9, 0, -3)]
    public void LocalNow_UsesSantiago(string instant, string date, int hour, int minute, int offset)
    {
        var utc = DateTimeOffset.Parse(instant, CultureInfo.InvariantCulture);
        var clock = new GymClock(new FakeTimeProvider(utc), Microsoft.Extensions.Options.Options.Create(new GymOptions()));
        Assert.Equal(utc, clock.UtcNow);
        Assert.Equal(DateOnly.Parse(date, CultureInfo.InvariantCulture), clock.Today);
        Assert.Equal(hour, clock.LocalNow.Hour);
        Assert.Equal(minute, clock.LocalNow.Minute);
        Assert.Equal(TimeSpan.FromHours(offset), clock.LocalNow.Offset);
        Assert.Equal(clock.Today.DayOfWeek, clock.LocalNow.DayOfWeek);
        if (date is "2026-11-17" or "2026-07-14")
            Assert.Equal(DayOfWeek.Tuesday, clock.LocalNow.DayOfWeek);
        Assert.Equal("America/Santiago", clock.TimeZone.Id);
    }

    [Theory]
    [InlineData("2026-07-14", -4)]
    [InlineData("2026-11-17", -3)]
    public void StartOfDay_UsesOffsetForRequestedDate(string date, int offset)
    {
        var clock = new GymClock(new FakeTimeProvider(), Microsoft.Extensions.Options.Options.Create(new GymOptions()));
        var day = DateOnly.Parse(date, CultureInfo.InvariantCulture);
        var start = clock.StartOfDay(day);
        Assert.Equal(day.ToDateTime(TimeOnly.MinValue), start.DateTime);
        Assert.Equal(TimeSpan.FromHours(offset), start.Offset);
    }

    [Fact]
    public void StartOfDay_WhenMidnightIsSkipped_ReturnsFirstValidInstant()
    {
        var clock = new GymClock(new FakeTimeProvider(), Microsoft.Extensions.Options.Options.Create(new GymOptions()));
        var start = clock.StartOfDay(new DateOnly(2026, 9, 6));
        Assert.Equal(new DateTime(2026, 9, 6, 1, 0, 0), start.DateTime);
        Assert.Equal(TimeSpan.FromHours(-3), start.Offset);
        Assert.False(clock.TimeZone.IsInvalidTime(start.DateTime));
    }

    [Fact]
    public async Task InvalidZone_FailsStartupValidation()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Gym:TimeZone"] = "Invalid/Zone", ["Jwt:Key"] = new string('x', 32) }).Build());
        services.AddGymOptions();
        await using var provider = services.BuildServiceProvider();
        var error = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains("Configure Gym:TimeZone con un identificador IANA válido (p. ej. America/Santiago).", error.Failures);
    }
}
