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

public sealed class QrTests(GymApiFactory factory)
{
    private static readonly DateOnly Date = new(2026, 11, 17);

    [Fact]
    public async Task IssuanceIsOwnerOnly_AndHasBlockWindowAndIdentity()
    {
        var user = await factory.CreateClientUserAsync();
        using var owner = user.Client;
        var other = await factory.CreateClientUserAsync();
        using var outsider = other.Client;
        var staff = await factory.CreateReceptionistClientAsync();
        using var receptionist = staff.Client;
        using var anonymous = factory.CreateClient();
        var slot = await factory.CreateSlotAsync(Date.DayOfWeek, new(10, 0), new(11, 0));
        var reservation = await factory.InsertReservationAsync(user.UserId, slot.Id, Date);
        var path = $"/api/v1/me/reservations/{reservation.Id}/qr";
        using (var denied = await anonymous.GetAsync(path, Ct)) Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        await Expect(await outsider.GetAsync(path, Ct), HttpStatusCode.NotFound);
        await Expect(await receptionist.GetAsync(path, Ct), HttpStatusCode.NotFound);
        var qr = await Expect(await owner.GetAsync(path, Ct), HttpStatusCode.OK);
        Assert.Equal(user.Rut, qr.GetProperty("user").GetProperty("rut").GetString());
        Assert.Equal(reservation.Id, qr.GetProperty("session").GetProperty("reservationId").GetInt32());
        Assert.Equal(new DateTimeOffset(2026, 11, 17, 12, 45, 0, TimeSpan.Zero), qr.GetProperty("validFrom").GetDateTimeOffset());
        Assert.Equal(new DateTimeOffset(2026, 11, 17, 14, 0, 0, TimeSpan.Zero), qr.GetProperty("validUntil").GetDateTimeOffset());
        Assert.True(AccessQrToken.TryRead(qr.GetProperty("token").GetString(), factory.QrKey, out var payload));
        Assert.Equal(user.UserId, payload.UserId);
        Assert.Equal(reservation.Id, payload.ReservationId);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(4499, true)]
    [InlineData(4500, false)]
    public async Task BlockWindowIsCheckedByValidateAndEntry(int secondsFromWindowStart, bool allowed)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 17, 12, 45, 0, TimeSpan.Zero).AddSeconds(secondsFromWindowStart));
        using var host = factory.WithClock(clock);
        var user = await factory.CreateClientUserAsync();
        using var owner = user.Client;
        var staff = await factory.CreateDedicatedAdminClientAsync(host);
        using var admin = staff.Client;
        var slot = await factory.CreateSlotAsync(Date.DayOfWeek, new(10, 0), new(11, 0));
        var reservation = await factory.InsertReservationAsync(user.UserId, slot.Id, Date);
        var token = await Api.Qr(owner, reservation.Id);
        var validation = await Expect(await Api.Access(admin, "validate", token), HttpStatusCode.OK);
        Assert.Equal(allowed, validation.GetProperty("canEnter").GetBoolean());
        if (!allowed) Assert.Equal(AccessQrToken.NotCurrent, validation.GetProperty("reason").GetString());
        await Expect(await Api.Access(admin, "entry", token), allowed ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
        if (allowed)
        {
            clock.Advance(TimeSpan.FromHours(3));
            await Expect(await admin.PostAsJsonAsync("/api/v1/access/exit", new { userId = user.UserId }, JsonOptions, Ct), HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task CancelledReservationRevokesPreviouslyIssuedQr()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 17, 12, 45, 0, TimeSpan.Zero));
        using var host = factory.WithClock(clock);
        var user = await factory.CreateClientUserAsync();
        using var original = user.Client;
        using var owner = host.CreateClient();
        owner.DefaultRequestHeaders.Authorization = original.DefaultRequestHeaders.Authorization;
        var staff = await factory.CreateDedicatedAdminClientAsync(host);
        using var admin = staff.Client;
        var slot = await factory.CreateSlotAsync(Date.DayOfWeek, new(10, 0), new(11, 0));
        var reservation = await factory.InsertReservationAsync(user.UserId, slot.Id, Date);
        var token = await Api.Qr(owner, reservation.Id);
        Assert.True((await Expect(await Api.Access(admin, "validate", token), HttpStatusCode.OK)).GetProperty("canEnter").GetBoolean());
        await Expect(await owner.DeleteAsync($"/api/v1/reservations/{reservation.Id}", Ct), HttpStatusCode.OK);
        await Expect(await owner.GetAsync($"/api/v1/me/reservations/{reservation.Id}/qr", Ct), HttpStatusCode.NotFound);
        Assert.False((await Expect(await Api.Access(admin, "validate", token), HttpStatusCode.OK)).GetProperty("canEnter").GetBoolean());
        await Expect(await Api.Access(admin, "entry", token), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AnotherBlockOfSameUserCannotAuthorizeCurrentBlock()
    {
        using var host = factory.WithClock(new FakeTimeProvider(new DateTimeOffset(2026, 11, 17, 13, 0, 0, TimeSpan.Zero)));
        var user = await factory.CreateClientUserAsync();
        using var owner = user.Client;
        var staff = await factory.CreateDedicatedAdminClientAsync(host);
        using var admin = staff.Client;
        var currentSlot = await factory.CreateSlotAsync(Date.DayOfWeek, new(10, 0), new(11, 0));
        var laterSlot = await factory.CreateSlotAsync(Date.DayOfWeek, new(12, 0), new(13, 0));
        await factory.InsertReservationAsync(user.UserId, currentSlot.Id, Date);
        var later = await factory.InsertReservationAsync(user.UserId, laterSlot.Id, Date);
        var token = await Api.Qr(owner, later.Id);
        Assert.False((await Expect(await Api.Access(admin, "validate", token), HttpStatusCode.OK)).GetProperty("canEnter").GetBoolean());
        await Expect(await Api.Access(admin, "entry", token), HttpStatusCode.BadRequest, AccessQrToken.NotCurrent);
        Assert.False(await factory.WithDbAsync(db => db.Visits.AnyAsync(v => v.UserId == user.UserId, Ct)));
    }

    [Fact]
    public async Task GroupQrRequiresCurrentBranch_BothAtIssuanceAndAtAccess()
    {
        using var host = factory.WithClock(new FakeTimeProvider(new DateTimeOffset(2026, 11, 17, 13, 0, 0, TimeSpan.Zero)));
        var staff = await factory.CreateDedicatedAdminClientAsync(host);
        using var admin = staff.Client;
        var user = await factory.CreateClientUserAsync();
        using var owner = user.Client;
        var slot = await factory.CreateSlotAsync(Date.DayOfWeek, new(10, 0), new(11, 0));
        var ids = await factory.WithDbAsync(async db =>
        {
            var branch = new SportBranch { Name = Guid.NewGuid().ToString("N") };
            var other = new SportBranch { Name = Guid.NewGuid().ToString("N") };
            db.SportBranches.AddRange(branch, other);
            await db.SaveChangesAsync(Ct);
            var person = await db.Users.SingleAsync(u => u.Id == user.UserId, Ct);
            person.Category = UserCategories.Deportista;
            person.SportBranchId = branch.Id;
            var group = new GroupBooking { SportBranchId = branch.Id, ScheduleSlotId = slot.Id, Date = Date, Mode = "Cupos", Seats = 2, CreatedById = staff.UserId };
            db.GroupBookings.Add(group);
            await db.SaveChangesAsync(Ct);
            return (Group: group.Id, Other: other.Id);
        });
        var path = $"/api/v1/me/group-bookings/{ids.Group}/qr";
        var token = (await Expect(await owner.GetAsync(path, Ct), HttpStatusCode.OK)).GetProperty("token").GetString()!;
        Assert.True((await Expect(await Api.Access(admin, "validate", token), HttpStatusCode.OK)).GetProperty("canEnter").GetBoolean());
        await factory.WithDbAsync(async db =>
        {
            var person = await db.Users.SingleAsync(u => u.Id == user.UserId, Ct);
            person.SportBranchId = ids.Other;
            await db.SaveChangesAsync(Ct);
        });
        await Expect(await owner.GetAsync(path, Ct), HttpStatusCode.NotFound);
        Assert.False((await Expect(await Api.Access(admin, "validate", token), HttpStatusCode.OK)).GetProperty("canEnter").GetBoolean());
        await Expect(await Api.Access(admin, "entry", token), HttpStatusCode.BadRequest);
    }
}
