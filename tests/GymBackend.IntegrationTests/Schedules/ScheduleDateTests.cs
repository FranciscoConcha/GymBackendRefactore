using System.Net;
using GymBackend.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Time.Testing;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Schedules;

public sealed class ScheduleDateTests(GymApiFactory factory)
{
    [Fact]
    public async Task Get_DefaultsToToday_FiltersDayAndOrdersStartTime()
    {
        using var real = await factory.CreateAdminClientAsync();
        using var host = factory.WithClock(new FakeTimeProvider(new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero)));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = real.DefaultRequestHeaders.Authorization;
        var late = await factory.CreateSlotAsync(DayOfWeek.Tuesday, new TimeOnly(15, 0), new TimeOnly(16, 0));
        var early = await factory.CreateSlotAsync(DayOfWeek.Tuesday, new TimeOnly(6, 0), new TimeOnly(7, 0));
        var otherDay = await factory.CreateSlotAsync(DayOfWeek.Wednesday, new TimeOnly(6, 0), new TimeOnly(7, 0));
        var today = await Expect(await client.GetAsync("/api/schedules", Ct), HttpStatusCode.OK);
        var slots = today.EnumerateArray().ToArray();
        Assert.Contains(slots, s => s.GetProperty("id").GetInt32() == early.Id);
        Assert.Contains(slots, s => s.GetProperty("id").GetInt32() == late.Id);
        Assert.DoesNotContain(slots, s => s.GetProperty("id").GetInt32() == otherDay.Id);
        Assert.All(slots, s =>
        {
            Assert.Equal("Martes", s.GetProperty("day").GetString());
            Assert.Equal("2026-11-17", s.GetProperty("date").GetString());
        });
        var starts = slots.Select(s => s.GetProperty("startTime").GetString()).ToArray();
        Assert.Equal(starts.OrderBy(s => s, StringComparer.Ordinal), starts);
        var tomorrow = await Expect(await client.GetAsync("/api/schedules?date=2026-11-18", Ct), HttpStatusCode.OK);
        Assert.Contains(tomorrow.EnumerateArray(), s => s.GetProperty("id").GetInt32() == otherDay.Id);
        Assert.All(tomorrow.EnumerateArray(), s =>
        {
            Assert.Equal("Miércoles", s.GetProperty("day").GetString());
            Assert.Equal("2026-11-18", s.GetProperty("date").GetString());
        });
    }

    [Fact]
    public async Task Get_Sunday_HasNoSeededBlocks()
    {
        using var client = await factory.CreateAdminClientAsync();
        var schedules = await Expect(await client.GetAsync("/api/schedules?date=2026-11-22", Ct), HttpStatusCode.OK);
        // La BD es compartida: otras pruebas pueden crear bloques dominicales con zona única
        // (p. ej. si la suite corre un sábado y reservan "mañana"). Solo se fija que la siembra no abre domingos
        // y que todo lo devuelto corresponde a domingo.
        Assert.DoesNotContain(schedules.EnumerateArray(), slot => slot.GetProperty("zone").GetString() == "Sala de Pesas");
        Assert.All(schedules.EnumerateArray(), slot => Assert.Equal("Domingo", slot.GetProperty("day").GetString()));
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("2026-02-30")]
    [InlineData("17-11-2026")]
    [InlineData("2026-1-7")]
    public async Task Get_InvalidDate_ExactMessage(string date)
    {
        using var client = await factory.CreateAdminClientAsync();
        await Expect(await client.GetAsync($"/api/schedules?date={date}", Ct), HttpStatusCode.BadRequest,
            "Fecha inválida. Use el formato yyyy-MM-dd.");
    }
}
