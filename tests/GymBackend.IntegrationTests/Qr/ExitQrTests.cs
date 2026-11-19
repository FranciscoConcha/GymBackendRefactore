using System.Net;
using System.Net.Http.Json;
using GymBackend.Application.Qr;
using GymBackend.Domain;
using GymBackend.IntegrationTests.Infrastructure;
using GymBackend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Qr;

public sealed class ExitQrTests(GymApiFactory factory)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EachPersonEntersAndExitsWithTheirOwnQr_AndCanReenter(bool group)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 17, 13, 0, 0, TimeSpan.Zero));
        using var host = factory.WithClock(clock);
        var staff = await factory.CreateDedicatedAdminClientAsync(host);
        using var admin = staff.Client;
        var first = await factory.CreateClientUserAsync();
        using var firstOwner = first.Client;
        var second = await factory.CreateClientUserAsync();
        using var secondOwner = second.Client;
        var slot = await factory.CreateSlotAsync(DayOfWeek.Tuesday, new(10, 0), new(11, 0));
        var date = new DateOnly(2026, 11, 17);
        string firstPath, secondPath;
        if (group)
        {
            var branchId = await factory.CreateBranchAsync();
            var groupId = await factory.WithDbAsync(async db =>
            {
                foreach (var id in new[] { first.UserId, second.UserId })
                {
                    var user = await db.Users.SingleAsync(u => u.Id == id, Ct);
                    user.Category = UserCategories.Deportista;
                    user.SportBranchId = branchId;
                }
                var booking = new GroupBooking
                {
                    SportBranchId = branchId,
                    ScheduleSlotId = slot.Id,
                    Date = date,
                    Mode = "Cupos",
                    Seats = 2,
                    CreatedById = staff.UserId
                };
                db.GroupBookings.Add(booking);
                await db.SaveChangesAsync(Ct);
                return booking.Id;
            });
            firstPath = secondPath = $"/api/v1/me/group-bookings/{groupId}/qr";
        }
        else
        {
            var r1 = await factory.InsertReservationAsync(first.UserId, slot.Id, date);
            var r2 = await factory.InsertReservationAsync(second.UserId, slot.Id, date);
            firstPath = $"/api/v1/me/reservations/{r1.Id}/qr";
            secondPath = $"/api/v1/me/reservations/{r2.Id}/qr";
        }
        var entry1 = await Expect(await firstOwner.GetAsync(firstPath, Ct), HttpStatusCode.OK);
        var entry2 = await Expect(await secondOwner.GetAsync(secondPath, Ct), HttpStatusCode.OK);
        Assert.Equal("Entrada", entry1.GetProperty("purpose").GetString());
        var token1 = entry1.GetProperty("token").GetString()!;
        var token2 = entry2.GetProperty("token").GetString()!;
        Assert.NotEqual(token1, token2);
        await Expect(await Api.Access(admin, "exit", token1), HttpStatusCode.BadRequest, "Escanee el QR de salida.");
        await Expect(await Api.Access(admin, "entry", token1), HttpStatusCode.OK);
        await Expect(await Api.Access(admin, "entry", token2), HttpStatusCode.OK);
        Assert.False((await Expect(await Api.Access(admin, "validate", token1), HttpStatusCode.OK)).GetProperty("canEnter").GetBoolean());
        var exit1 = await Expect(await firstOwner.GetAsync(firstPath, Ct), HttpStatusCode.OK);
        var exit2 = await Expect(await secondOwner.GetAsync(secondPath, Ct), HttpStatusCode.OK);
        Assert.Equal("Salida", exit1.GetProperty("purpose").GetString());
        Assert.Equal("Salida", exit2.GetProperty("purpose").GetString());
        var qr1 = exit1.GetProperty("token").GetString()!;
        var qr2 = exit2.GetProperty("token").GetString()!;
        Assert.NotEqual(qr1, qr2);
        Assert.True(AccessQrToken.TryRead(qr1, factory.QrKey, out var payload));
        Assert.Equal(first.UserId, payload.UserId);
        Assert.Equal(TimeSpan.FromHours(18).TotalSeconds, payload.Expires - payload.NotBefore);
        var validation = await Expect(await Api.Access(admin, "validate", qr1), HttpStatusCode.OK);
        Assert.Equal("Salida", validation.GetProperty("action").GetString());
        Assert.True(validation.GetProperty("canExit").GetBoolean());
        Assert.True(await factory.WithDbAsync(db => db.Visits.AnyAsync(v => v.Id == payload.VisitId && v.ExitAt == null, Ct)));
        await Expect(await admin.PostAsJsonAsync("/api/v1/access/exit", new { token = qr1, userId = second.UserId }, JsonOptions, Ct), HttpStatusCode.BadRequest);
        await Expect(await admin.PostAsJsonAsync("/api/v1/access/exit", new { }, JsonOptions, Ct), HttpStatusCode.BadRequest);
        var closed = await Expect(await Api.Access(admin, "exit", qr1), HttpStatusCode.OK);
        Assert.Equal(first.UserId, closed.GetProperty("visit").GetProperty("userId").GetInt32());
        Assert.True(await factory.WithDbAsync(db => db.Visits.AnyAsync(v => v.UserId == second.UserId && v.ExitAt == null, Ct)));
        await Expect(await Api.Access(admin, "exit", qr1), HttpStatusCode.BadRequest, "Este QR de salida ya fue utilizado.");
        var used = await Expect(await Api.Access(admin, "validate", qr1), HttpStatusCode.OK);
        Assert.False(used.GetProperty("canExit").GetBoolean());
        Assert.Equal("Este QR de salida ya fue utilizado.", used.GetProperty("reason").GetString());
        await Expect(await Api.Access(admin, "exit", qr2), HttpStatusCode.OK);
        var again = await Expect(await firstOwner.GetAsync(firstPath, Ct), HttpStatusCode.OK);
        Assert.Equal("Entrada", again.GetProperty("purpose").GetString());
        clock.Advance(TimeSpan.FromSeconds(1));
        await Expect(await Api.Access(admin, "entry", again.GetProperty("token").GetString()!), HttpStatusCode.OK);
        await Expect(await Api.Access(admin, "exit", qr1), HttpStatusCode.BadRequest);
        await Expect(await Exit(admin, first.UserId), HttpStatusCode.OK);
        var events = await factory.WithDbAsync(db => db.AccessEvents.Where(e => e.Action == "Salida" &&
            e.Result == "Aceptado" && (e.UserId == first.UserId || e.UserId == second.UserId)).OrderBy(e => e.Id).ToArrayAsync(Ct));
        Assert.Equal(new[] { "QR", "QR", "Manual" }, events.Select(e => e.Reason));
    }

    [Fact]
    public async Task ExitRemainsAvailableAfterBlockEndAndAccountDisabled_AndIsIdempotent()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 17, 13, 0, 0, TimeSpan.Zero));
        using var host = factory.WithClock(clock);
        var staff = await factory.CreateDedicatedAdminClientAsync(host);
        using var admin = staff.Client;
        var person = await factory.CreateClientUserAsync();
        using var owner = person.Client;
        var slot = await factory.CreateSlotAsync(DayOfWeek.Tuesday, new(10, 0), new(11, 0));
        var reservation = await factory.InsertReservationAsync(person.UserId, slot.Id, new(2026, 11, 17));
        await Expect(await Api.Access(admin, "entry", await Api.Qr(owner, reservation.Id)), HttpStatusCode.OK);
        var exit = await Api.Qr(owner, reservation.Id);
        clock.Advance(TimeSpan.FromHours(2));
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Id == person.UserId, Ct)).Enabled = false;
            await db.SaveChangesAsync(Ct);
        });
        var validation = await Expect(await Api.Access(admin, "validate", exit), HttpStatusCode.OK);
        Assert.True(validation.GetProperty("canExit").GetBoolean());
        admin.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var original = await Expect(await Api.Access(admin, "exit", exit), HttpStatusCode.OK);
        var replay = await Expect(await Api.Access(admin, "exit", exit), HttpStatusCode.OK);
        Assert.Equal(original.GetRawText(), replay.GetRawText());
        Assert.Equal(1, await factory.WithDbAsync(db => db.AccessEvents.CountAsync(e => e.UserId == person.UserId &&
            e.Action == "Salida" && e.Reason == "QR", Ct)));
    }
}
