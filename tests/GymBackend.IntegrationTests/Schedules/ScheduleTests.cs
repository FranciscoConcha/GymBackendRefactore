using System.Net;
using GymBackend.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Schedules;

public sealed class ScheduleTests(GymApiFactory factory)
{
    [Fact]
    public async Task Get_PermisosBloquesYReservas_ComportamientoActual()
    {
        using var anonymous = factory.CreateClient();
        using var denied = await anonymous.GetAsync("/api/schedules", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        var first = await factory.CreateClientUserAsync();
        using var one = first.Client;
        var second = await factory.CreateClientUserAsync();
        using var two = second.Client;
        var slot = await factory.CreateSlotAsync("Lunes", "08:00", "09:00");
        var disabled = await factory.CreateSlotAsync("Lunes", "08:00", "09:00");
        await factory.WithDbAsync(async db =>
        {
            (await db.ScheduleSlots.SingleAsync(s => s.Id == disabled.Id, Ct)).Enabled = false;
            await db.SaveChangesAsync(Ct);
        });
        var reservation = await Reservation(one, slot);
        await Reservation(two, slot);
        await Expect(await one.DeleteAsync($"/api/reservations/{reservation.GetProperty("id").GetInt32()}", Ct), HttpStatusCode.OK);
        var slots = (await Expect(await one.GetAsync("/api/schedules", Ct), HttpStatusCode.OK)).EnumerateArray().ToArray();
        Assert.DoesNotContain(slots, s => s.GetProperty("id").GetInt32() == disabled.Id);
        var own = Assert.Single(slots, s => s.GetProperty("id").GetInt32() == slot.Id);
        Assert.Equal(1, own.GetProperty("reservedCount").GetInt32());
    }
}
