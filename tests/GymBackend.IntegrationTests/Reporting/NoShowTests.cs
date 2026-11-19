using System.Net;
using GymBackend.Application.Alerts;
using GymBackend.Domain;
using GymBackend.IntegrationTests.Infrastructure;
using GymBackend.Models;
using GymBackend.Time;
using Microsoft.EntityFrameworkCore;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Reporting;

public sealed class NoShowTests(GymApiFactory factory)
{
    [Fact]
    public async Task CancelledAndBriefVisitsDoNotBecomeNoShows_JobAndAcknowledgementAreIdempotent()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        var outsider = await factory.CreateClientUserAsync();
        using var otherClient = outsider.Client;
        var date = new DateOnly(2020, 1, 6);
        var slot = await factory.CreateSlotAsync(date.DayOfWeek, new(8, 0), new(9, 0));
        var cancelled = await factory.InsertReservationAsync(user.UserId, slot.Id, date, ReservationStatus.Cancelled);
        var brief = await factory.InsertReservationAsync(user.UserId, slot.Id, date.AddDays(7));
        var absent = await factory.InsertReservationAsync(user.UserId, slot.Id, date.AddDays(14));
        await factory.WithDbAsync(async db =>
        {
            db.Visits.Add(new Visit
            {
                UserId = user.UserId,
                ReservationId = brief.Id,
                Date = brief.Date,
                EntryAt = new DateTime(2020, 1, 13, 11, 0, 0, DateTimeKind.Utc),
                ExitAt = new DateTime(2020, 1, 13, 11, 0, 5, DateTimeKind.Utc),
                EntryOperatorId = user.UserId,
                ExitOperatorId = user.UserId,
                DocumentType = "TNE"
            });
            await db.SaveChangesAsync(Ct);
            var processor = new NoShowProcessor(db, new FixedClock());
            await processor.RunAsync(Ct);
            await processor.RunAsync(Ct);
        });
        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(ReservationStatus.Cancelled, (await db.Reservations.SingleAsync(r => r.Id == cancelled.Id, Ct)).Status);
            Assert.NotEqual(ReservationStatus.NoShow, (await db.Reservations.SingleAsync(r => r.Id == brief.Id, Ct)).Status);
            Assert.False(await db.AttendanceAlerts.AnyAsync(a => a.ReservationId == cancelled.Id || a.ReservationId == brief.Id, Ct));
            Assert.Equal(ReservationStatus.NoShow, (await db.Reservations.SingleAsync(r => r.Id == absent.Id, Ct)).Status);
        });
        var alert = await factory.WithDbAsync(db => db.AttendanceAlerts.SingleAsync(a => a.ReservationId == absent.Id, Ct));
        await Expect(await otherClient.PostAsync($"/api/v1/me/alerts/{alert.Id}/acknowledge", null, Ct), HttpStatusCode.NotFound);
        await Expect(await client.PostAsync($"/api/v1/me/alerts/{alert.Id}/acknowledge", null, Ct), HttpStatusCode.OK);
        await Expect(await client.PostAsync($"/api/v1/me/alerts/{alert.Id}/acknowledge", null, Ct), HttpStatusCode.OK);
        var alerts = await Expect(await client.GetAsync("/api/v1/me/alerts", Ct), HttpStatusCode.OK);
        Assert.Single(alerts.EnumerateArray(), a => a.GetProperty("reservationId").GetInt32() == absent.Id);
        Assert.Equal(ReservationStatus.NoShow, await factory.WithDbAsync(db => db.Reservations.Where(r => r.Id == absent.Id).Select(r => r.Status).SingleAsync(Ct)));
    }

    private sealed class FixedClock : IGymClock
    {
        public DateTimeOffset UtcNow => new(2021, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public TimeZoneInfo TimeZone => TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");
        public DateTimeOffset LocalNow => TimeZoneInfo.ConvertTime(UtcNow, TimeZone);
        public DateOnly Today => DateOnly.FromDateTime(LocalNow.DateTime);
        public DateTimeOffset StartOfDay(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), TimeZone.GetUtcOffset(date.ToDateTime(TimeOnly.MinValue)));
    }
}
