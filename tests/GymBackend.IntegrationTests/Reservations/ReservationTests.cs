using System.Net;
using GymBackend.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Reservations;

public sealed class ReservationTests(GymApiFactory factory)
{
    [Fact]
    public async Task Get_Permisos_ComportamientoActual()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        var other = await factory.CreateClientUserAsync();
        using var otherClient = other.Client;
        using var admin = await factory.CreateAdminClientAsync();
        using var anonymous = factory.CreateClient();
        var slot = await factory.CreateSlotAsync("Lunes", "08:00", "09:00");
        var reservation = await Reservation(client, slot);
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
        var slot = await factory.CreateSlotAsync("Lunes", "08:00", "09:00", 7);
        using var anonymous = factory.CreateClient();
        using var denied = await Reserve(anonymous, slot);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        var missing = new GymBackend.Models.ScheduleSlot { Day = slot.Day, StartTime = slot.StartTime, EndTime = slot.EndTime, Zone = Guid.NewGuid().ToString() };
        await Expect(await Reserve(client, missing), HttpStatusCode.BadRequest, "El bloque seleccionado no está disponible.");
        using var ghost = factory.CreateClientWithToken(factory.CreateJwt(999999, "Cliente", TestData.NextRut()));
        await Expect(await Reserve(ghost, slot), HttpStatusCode.Unauthorized, "La sesión no corresponde a un usuario válido.");
        var result = await Reservation(client, slot);
        Assert.Equal("Reservado", result.GetProperty("status").GetString());
        Assert.Equal(7, result.GetProperty("capacity").GetInt32());
        Assert.Equal($"Test {user.Rut}", result.GetProperty("userName").GetString());
        await Expect(await Reserve(client, slot), HttpStatusCode.BadRequest, "Ya tienes una reserva para este bloque.");
    }

    [Fact]
    public async Task Post_CupoLlenoYCancelacion_LiberaCupo()
    {
        var slot = await factory.CreateSlotAsync("Martes", "08:00", "09:00", 2);
        var first = await factory.CreateClientUserAsync();
        using var one = first.Client;
        var second = await factory.CreateClientUserAsync();
        using var two = second.Client;
        var third = await factory.CreateClientUserAsync();
        using var three = third.Client;
        var reservation = await Reservation(one, slot);
        await Reservation(two, slot);
        await Expect(await Reserve(three, slot), HttpStatusCode.BadRequest, "No quedan cupos disponibles para este bloque.");
        await Expect(await one.DeleteAsync($"/api/reservations/{reservation.GetProperty("id").GetInt32()}", Ct), HttpStatusCode.OK);
        await Reservation(three, slot);
    }

    [Fact]
    [Trait("Category", "LegacyBehavior")]
    public async Task Post_UsuarioDeshabilitado_Ok()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        using var admin = await factory.CreateAdminClientAsync();
        await Expect(await Status(admin, user.UserId, false), HttpStatusCode.OK);
        var slot = await factory.CreateSlotAsync("Lunes", "08:00", "09:00");
        // Comportamiento actual (D2); cambia en F5.
        await Reservation(client, slot);
    }

    [Fact]
    public async Task Delete_PermisosYEstado_ComportamientoActual()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        var other = await factory.CreateClientUserAsync();
        using var otherClient = other.Client;
        using var admin = await factory.CreateAdminClientAsync();
        using var anonymous = factory.CreateClient();
        var slot = await factory.CreateSlotAsync("Lunes", "08:00", "09:00");
        var id = (await Reservation(client, slot)).GetProperty("id").GetInt32();
        using var denied = await anonymous.DeleteAsync($"/api/reservations/{id}", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        await Expect(await client.DeleteAsync($"/api/reservations/{int.MaxValue}", Ct), HttpStatusCode.NotFound, "Reserva no encontrada.");
        using var forbidden = await otherClient.DeleteAsync($"/api/reservations/{id}", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var cancelled = await Expect(await client.DeleteAsync($"/api/reservations/{id}", Ct), HttpStatusCode.OK, "Reserva cancelada correctamente.");
        Assert.Equal("Cancelado", cancelled.GetProperty("reservation").GetProperty("status").GetString());
        var nextId = (await Reservation(client, slot)).GetProperty("id").GetInt32();
        var byAdmin = await Expect(await admin.DeleteAsync($"/api/reservations/{nextId}", Ct), HttpStatusCode.OK, "Reserva cancelada correctamente.");
        Assert.Equal("Cancelado", byAdmin.GetProperty("reservation").GetProperty("status").GetString());
    }

    [Fact]
    [Trait("Category", "LegacyBehavior")]
    public async Task Delete_Asistio_Ok()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        var slot = await factory.CreateSlotAsync("Lunes", "08:00", "09:00");
        var id = (await Reservation(client, slot)).GetProperty("id").GetInt32();
        await factory.WithDbAsync(async db =>
        {
            var reservation = await db.Reservations.SingleAsync(r => r.Id == id, Ct);
            reservation.Status = "Asistió";
            await db.SaveChangesAsync(Ct);
        });
        // Comportamiento actual (D13); cambia en F5.
        var json = await Expect(await client.DeleteAsync($"/api/reservations/{id}", Ct), HttpStatusCode.OK, "Reserva cancelada correctamente.");
        Assert.Equal("Cancelado", json.GetProperty("reservation").GetProperty("status").GetString());
    }
}
