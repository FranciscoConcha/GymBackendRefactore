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
        var date = GymTime.Tomorrow();
        using var anonymous = factory.CreateClient();
        using var denied = await anonymous.GetAsync("/api/v1/schedules", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        var first = await factory.CreateClientUserAsync();
        using var one = first.Client;
        var second = await factory.CreateClientUserAsync();
        using var two = second.Client;
        var slot = await factory.CreateSlotAsync(date.DayOfWeek, new TimeOnly(8, 0), new TimeOnly(9, 0));
        var disabled = await factory.CreateSlotAsync(date.DayOfWeek, new TimeOnly(8, 0), new TimeOnly(9, 0));
        await factory.WithDbAsync(async db =>
        {
            (await db.ScheduleSlots.SingleAsync(s => s.Id == disabled.Id, Ct)).Enabled = false;
            await db.SaveChangesAsync(Ct);
        });
        var reservation = await Reservation(one, slot.Id, date);
        await Reservation(two, slot.Id, date);
        await Expect(await one.DeleteAsync($"/api/v1/reservations/{reservation.GetProperty("id").GetInt32()}", Ct), HttpStatusCode.OK);
        var slots = (await Expect(await one.GetAsync($"/api/v1/schedules?date={date:yyyy-MM-dd}", Ct), HttpStatusCode.OK)).EnumerateArray().ToArray();
        Assert.DoesNotContain(slots, s => s.GetProperty("id").GetInt32() == disabled.Id);
        var own = Assert.Single(slots, s => s.GetProperty("id").GetInt32() == slot.Id);
        Assert.Equal(1, own.GetProperty("reservedCount").GetInt32());
    }
}
