using System.Globalization;
using System.Net;
using GymBackend.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Time;

public sealed class GymTimeTests(GymApiFactory factory)
{
    [Fact]
    public async Task LateUtcDate_UsesLocalTuesday_AndStoresUtcEntryAndExit()
    {
        var instant = new DateTimeOffset(2026, 11, 18, 2, 50, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(instant);
        using var host = factory.WithClock(clock);
        var user = await factory.CreateClientUserAsync();
        using var realClient = user.Client;
        using var realAdmin = await factory.CreateAdminClientAsync();
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = realClient.DefaultRequestHeaders.Authorization;
        using var admin = host.CreateClient();
        admin.DefaultRequestHeaders.Authorization = realAdmin.DefaultRequestHeaders.Authorization;
        var slot = await factory.CreateSlotAsync(DayOfWeek.Tuesday, new TimeOnly(23, 0), new TimeOnly(23, 59));
        var qrReservation = await factory.InsertReservationAsync(user.UserId, slot.Id, new DateOnly(2026, 11, 17));
        var qr = await Expect(await client.GetAsync($"/api/v1/me/reservations/{qrReservation.Id}/qr", Ct), HttpStatusCode.OK);
        Assert.Equal("2026-11-17", qr.GetProperty("session").GetProperty("date").GetString());
        var validFrom = qr.GetProperty("validFrom").GetDateTimeOffset();
        Assert.Equal(new DateTimeOffset(2026, 11, 18, 1, 45, 0, TimeSpan.Zero), validFrom);
        Assert.Equal(TimeSpan.Zero, validFrom.Offset);
        var validUntil = qr.GetProperty("validUntil").GetDateTimeOffset();
        Assert.Equal(new DateTimeOffset(2026, 11, 18, 2, 59, 0, TimeSpan.Zero), validUntil);
        Assert.Equal(TimeSpan.Zero, validUntil.Offset);
        var token = qr.GetProperty("token").GetString()!;
        var entry = await Expect(await Api.Access(admin, "entry", token), HttpStatusCode.OK);
        var id = entry.GetProperty("visit").GetProperty("id").GetInt32();
        clock.Advance(TimeSpan.FromMinutes(1));
        await Expect(await Api.Exit(admin, user.UserId), HttpStatusCode.OK);
        await factory.WithDbAsync(async db =>
        {
            var attendance = await db.Visits.SingleAsync(a => a.Id == id, Ct);
            Assert.Equal(new DateOnly(2026, 11, 17), attendance.Date);
            Assert.Equal(instant.UtcDateTime, attendance.EntryAt);
            Assert.Equal(clock.GetUtcNow().UtcDateTime, attendance.ExitAt);
            Assert.Equal(DateTimeKind.Utc, attendance.ExitAt!.Value.Kind);
        });
    }

    [Theory]
    [InlineData("2026-11-17T10:44:00Z", "2026-11-17T10:45:00Z")]
    [InlineData("2026-11-17T12:30:00Z", "2026-11-17T12:29:59Z")] // fin exclusivo: 09:30 local rechazada, 09:29:59 admitida
    [InlineData("2026-07-14T11:44:00Z", "2026-07-14T11:45:00Z")]
    public async Task EntryWindow_FifteenMinutesBeforeStartUntilEnd(string rejected, string accepted)
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse(rejected, CultureInfo.InvariantCulture));
        using var host = factory.WithClock(clock);
        var user = await factory.CreateClientUserAsync();
        using var realClient = user.Client;
        using var realAdmin = await factory.CreateAdminClientAsync();
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = realClient.DefaultRequestHeaders.Authorization;
        using var admin = host.CreateClient();
        admin.DefaultRequestHeaders.Authorization = realAdmin.DefaultRequestHeaders.Authorization;
        var slot = await factory.CreateSlotAsync(DayOfWeek.Tuesday, new TimeOnly(8, 0), new TimeOnly(9, 30));
        var local = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("America/Santiago"));
        var qrReservation = await factory.InsertReservationAsync(user.UserId, slot.Id, DateOnly.FromDateTime(local.DateTime));
        var token = await Api.Qr(client, qrReservation.Id);
        await Expect(await Api.Access(admin, "entry", token), HttpStatusCode.BadRequest,
            "El código QR no está vigente para este bloque.");
        using var acceptedHost = factory.WithClock(new FakeTimeProvider(
            DateTimeOffset.Parse(accepted, CultureInfo.InvariantCulture)));
        using var acceptedAdmin = acceptedHost.CreateClient();
        acceptedAdmin.DefaultRequestHeaders.Authorization = realAdmin.DefaultRequestHeaders.Authorization;
        await Expect(await Api.Access(acceptedAdmin, "entry", token), HttpStatusCode.OK);
    }
}
