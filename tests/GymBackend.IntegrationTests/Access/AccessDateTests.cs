using System.Net;
using GymBackend.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Time.Testing;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Access;

public sealed class AccessDateTests(GymApiFactory factory)
{
    [Theory]
    [InlineData(-7)]
    [InlineData(7)]
    public async Task Entry_RequiresReservationForToday(int otherDateOffset)
    {
        var user = await factory.CreateClientUserAsync();
        using var real = user.Client;
        using var realAdmin = await factory.CreateAdminClientAsync();
        using var host = factory.WithClock(new FakeTimeProvider(new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero)));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = real.DefaultRequestHeaders.Authorization;
        using var admin = host.CreateClient();
        admin.DefaultRequestHeaders.Authorization = realAdmin.DefaultRequestHeaders.Authorization;
        var today = new DateOnly(2026, 11, 17);
        var slot = await factory.CreateSlotAsync(DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(10, 0));
        var other = await factory.InsertReservationAsync(user.UserId, slot.Id, today.AddDays(otherDateOffset));
        var token = await Api.Qr(client, other.Id);
        await Expect(await Api.Access(admin, "entry", token), HttpStatusCode.BadRequest,
            "El código QR no está vigente para este bloque.");
        var current = await factory.InsertReservationAsync(user.UserId, slot.Id, today);
        await Expect(await Api.Access(admin, "entry", token), HttpStatusCode.BadRequest);
        var currentToken = await Api.Qr(client, current.Id);
        var entry = await Expect(await Api.Access(admin, "entry", currentToken), HttpStatusCode.OK, "Entrada registrada correctamente.");
        var attendance = entry.GetProperty("visit");
        Assert.Equal(current.Id, attendance.GetProperty("reservationId").GetInt32());
        Assert.NotEqual(other.Id, attendance.GetProperty("reservationId").GetInt32());
        Assert.Equal("2026-11-17", attendance.GetProperty("date").GetString());
        Assert.Equal(user.Rut, attendance.GetProperty("rut").GetString());
        Assert.Equal($"Test {user.Rut}", attendance.GetProperty("fullName").GetString());
    }
}
