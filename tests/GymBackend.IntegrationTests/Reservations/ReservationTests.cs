using GymBackend.Domain;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using GymBackend.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Reservations;

public sealed class ReservationTests(GymApiFactory factory)
{
    [Fact]
    public async Task Get_Permisos_ComportamientoActual()
    {
        var date = GymTime.Tomorrow();
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        var other = await factory.CreateClientUserAsync();
        using var otherClient = other.Client;
        using var admin = await factory.CreateAdminClientAsync();
        using var anonymous = factory.CreateClient();
        var slot = await factory.CreateSlotAsync(date.DayOfWeek, new TimeOnly(8, 0), new TimeOnly(9, 0));
        var reservation = await Reservation(client, slot.Id, date);
        using var denied = await client.GetAsync("/api/reservations", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var all = await Expect(await admin.GetAsync("/api/reservations", Ct), HttpStatusCode.OK);
        Assert.Contains(all.EnumerateArray(), r => r.GetProperty("id").GetInt32() == reservation.GetProperty("id").GetInt32());
        foreach (var path in new[] { "/api/reservations", $"/api/reservations/user/{user.UserId}" })
        {
            using var response = await anonymous.GetAsync(path, Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        var own = await Expect(await client.GetAsync($"/api/reservations/user/{user.UserId}", Ct), HttpStatusCode.OK);
        Assert.Contains(own.EnumerateArray(), r => r.GetProperty("id").GetInt32() == reservation.GetProperty("id").GetInt32());
        using var forbidden = await otherClient.GetAsync($"/api/reservations/user/{user.UserId}", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        await Expect(await admin.GetAsync($"/api/reservations/user/{user.UserId}", Ct), HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_ValidacionesYDuplicado_ComportamientoActual()
    {
        var date = GymTime.Tomorrow();
        var slot = await factory.CreateSlotAsync(date.DayOfWeek, new TimeOnly(8, 0), new TimeOnly(9, 0), 7);
        using var anonymous = factory.CreateClient();
        using var denied = await Reserve(anonymous, slot.Id, date);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        var missing = new GymBackend.Models.ScheduleSlot { Day = slot.Day, StartTime = slot.StartTime, EndTime = slot.EndTime, Zone = Guid.NewGuid().ToString() };
        await Expect(await Reserve(client, missing.Id, date), HttpStatusCode.BadRequest, "El bloque seleccionado no está disponible.");
        using var ghost = factory.CreateClientWithToken(factory.CreateJwt(999999, "Cliente", TestData.NextRut()));
        await Expect(await Reserve(ghost, slot.Id, date), HttpStatusCode.Unauthorized, "La sesión no corresponde a un usuario válido.");
        var result = await Reservation(client, slot.Id, date);
        Assert.Equal("Reservado", result.GetProperty("status").GetString());
        Assert.Equal(7, result.GetProperty("capacity").GetInt32());
        Assert.Equal($"Test {user.Rut}", result.GetProperty("userName").GetString());
        await Expect(await Reserve(client, slot.Id, date), HttpStatusCode.BadRequest, "Ya tienes una reserva para este bloque.");
    }

    [Fact]
    public async Task Post_CupoLlenoYCancelacion_LiberaCupo()
    {
        var date = GymTime.Tomorrow();
        var slot = await factory.CreateSlotAsync(date.DayOfWeek, new TimeOnly(8, 0), new TimeOnly(9, 0), 2);
        var first = await factory.CreateClientUserAsync();
        using var one = first.Client;
        var second = await factory.CreateClientUserAsync();
        using var two = second.Client;
        var third = await factory.CreateClientUserAsync();
        using var three = third.Client;
        var reservation = await Reservation(one, slot.Id, date);
        await Reservation(two, slot.Id, date);
        await Expect(await Reserve(three, slot.Id, date), HttpStatusCode.BadRequest, "No quedan cupos disponibles para este bloque.");
        await Expect(await one.DeleteAsync($"/api/reservations/{reservation.GetProperty("id").GetInt32()}", Ct), HttpStatusCode.OK);
        await Reservation(three, slot.Id, date);
    }

    [Fact]
    public async Task Post_UsuarioDeshabilitado_Unauthorized()
    {
        var date = GymTime.Tomorrow();
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        using var admin = await factory.CreateAdminClientAsync();
        await Expect(await Status(admin, user.UserId, false), HttpStatusCode.OK);
        var slot = await factory.CreateSlotAsync(date.DayOfWeek, new TimeOnly(8, 0), new TimeOnly(9, 0));
        // D2 corregido en F5: el token previo de un usuario deshabilitado deja de ser válido.
        using var response = await client.PostAsJsonAsync("/api/reservations",
            new { scheduleSlotId = slot.Id, date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) }, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_PermisosYEstado_ComportamientoActual()
    {
        var date = GymTime.Tomorrow();
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        var other = await factory.CreateClientUserAsync();
        using var otherClient = other.Client;
        using var admin = await factory.CreateAdminClientAsync();
        using var anonymous = factory.CreateClient();
        var slot = await factory.CreateSlotAsync(date.DayOfWeek, new TimeOnly(8, 0), new TimeOnly(9, 0));
        var id = (await Reservation(client, slot.Id, date)).GetProperty("id").GetInt32();
        using var denied = await anonymous.DeleteAsync($"/api/reservations/{id}", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        await Expect(await client.DeleteAsync($"/api/reservations/{int.MaxValue}", Ct), HttpStatusCode.NotFound, "Reserva no encontrada.");
        using var forbidden = await otherClient.DeleteAsync($"/api/reservations/{id}", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var cancelled = await Expect(await client.DeleteAsync($"/api/reservations/{id}", Ct), HttpStatusCode.OK, "Reserva cancelada correctamente.");
        Assert.Equal("Cancelado", cancelled.GetProperty("reservation").GetProperty("status").GetString());
        var nextId = (await Reservation(client, slot.Id, date)).GetProperty("id").GetInt32();
        var byAdmin = await Expect(await admin.DeleteAsync($"/api/reservations/{nextId}", Ct), HttpStatusCode.OK, "Reserva cancelada correctamente.");
        Assert.Equal("Cancelado", byAdmin.GetProperty("reservation").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Delete_Asistio_Rechazado()
    {
        var date = GymTime.Tomorrow();
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        var slot = await factory.CreateSlotAsync(date.DayOfWeek, new TimeOnly(8, 0), new TimeOnly(9, 0));
        var id = (await Reservation(client, slot.Id, date)).GetProperty("id").GetInt32();
        await factory.WithDbAsync(async db =>
        {
            var reservation = await db.Reservations.SingleAsync(r => r.Id == id, Ct);
            reservation.Status = ReservationStatus.Attended;
            await db.SaveChangesAsync(Ct);
        });
        await Expect(await client.DeleteAsync($"/api/reservations/{id}", Ct), HttpStatusCode.BadRequest, "Solo se pueden cancelar reservas activas.");
    }
}
