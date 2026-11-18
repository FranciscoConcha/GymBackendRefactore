using System.Globalization;
using System.Net;
using GymBackend.Domain;
using GymBackend.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Time.Testing;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Reservations;

public sealed class ReservationDateTests(GymApiFactory factory)
{
    private static readonly DateOnly Today = new(2026, 11, 17);
    private static FakeTimeProvider Clock() => new(new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero));

    [Theory]
    [InlineData("2026-11-18", DayOfWeek.Tuesday, 10, 0, "La fecha no corresponde al día del bloque.")]
    [InlineData("2026-11-16", DayOfWeek.Monday, 10, 0, "El bloque ya comenzó o la fecha ya pasó.")]
    [InlineData("2026-11-17", DayOfWeek.Tuesday, 9, 0, "El bloque ya comenzó o la fecha ya pasó.")]
    [InlineData("2026-11-17", DayOfWeek.Tuesday, 8, 59, "El bloque ya comenzó o la fecha ya pasó.")]
    [InlineData("2026-11-25", DayOfWeek.Wednesday, 10, 0, "La fecha excede el plazo máximo de reserva.")]
    public async Task Create_InvalidDate_ExactMessage(string date, DayOfWeek day, int hour, int minute, string message)
    {
        var user = await factory.CreateClientUserAsync();
        using var real = user.Client;
        using var host = factory.WithClock(Clock());
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = real.DefaultRequestHeaders.Authorization;
        var slot = await factory.CreateSlotAsync(day, new TimeOnly(hour, minute), new TimeOnly(11, 0));
        await Expect(await Reserve(client, slot.Id, DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture)), HttpStatusCode.BadRequest, message);
    }

    [Theory]
    [InlineData(0, 9, 1)]
    [InlineData(7, 10, 0)]
    public async Task Create_BeforeStartAndExactHorizon_Succeeds(int daysAhead, int hour, int minute)
    {
        var user = await factory.CreateClientUserAsync();
        using var real = user.Client;
        var clock = Clock();
        using var host = factory.WithClock(clock);
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = real.DefaultRequestHeaders.Authorization;
        var date = Today.AddDays(daysAhead);
        var slot = await factory.CreateSlotAsync(date.DayOfWeek, new TimeOnly(hour, minute), new TimeOnly(11, 0));
        var reservation = await Reservation(client, slot.Id, date);
        Assert.Equal(slot.Id, reservation.GetProperty("scheduleSlotId").GetInt32());
        Assert.Equal(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), reservation.GetProperty("date").GetString());
        Assert.Equal(clock.GetUtcNow().UtcDateTime, reservation.GetProperty("createdAt").GetDateTime());
    }

    [Fact]
    public async Task Create_DisabledSlot_RejectedBeforeDateValidation()
    {
        var user = await factory.CreateClientUserAsync();
        using var real = user.Client;
        using var host = factory.WithClock(Clock());
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = real.DefaultRequestHeaders.Authorization;
        var slot = await factory.CreateSlotAsync(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(11, 0));
        await factory.WithDbAsync(async db =>
        {
            var stored = await db.ScheduleSlots.FindAsync(new object[] { slot.Id }, Ct);
            stored!.Enabled = false;
            await db.SaveChangesAsync(Ct);
        });
        await Expect(await Reserve(client, slot.Id, Today), HttpStatusCode.BadRequest, "El bloque seleccionado no está disponible.");
    }

    [Theory]
    [InlineData("2026-11-26", true)]
    [InlineData("2026-11-27", false)]
    public async Task Create_BusinessDays_UsesConfiguredHorizon(string dateText, bool allowed)
    {
        var user = await factory.CreateClientUserAsync();
        using var real = user.Client;
        using var host = factory.WithClockAndSettings(Clock(), new Dictionary<string, string?>
        {
            ["Reservations:HorizonUnit"] = "BusinessDays",
            ["Reservations:MaxDaysAhead"] = "7",
        });
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = real.DefaultRequestHeaders.Authorization;
        var date = DateOnly.ParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var slot = await factory.CreateSlotAsync(date.DayOfWeek, new TimeOnly(10, 0), new TimeOnly(11, 0));
        await Expect(await Reserve(client, slot.Id, date), allowed ? HttpStatusCode.OK : HttpStatusCode.BadRequest,
            allowed ? "Reserva creada correctamente." : "La fecha excede el plazo máximo de reserva.");
    }

    [Fact]
    public async Task Create_CapacityAndDuplicate_AreScopedToDate()
    {
        var user = await factory.CreateClientUserAsync();
        using var real = user.Client;
        using var host = factory.WithClock(Clock());
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = real.DefaultRequestHeaders.Authorization;
        var slot = await factory.CreateSlotAsync(DayOfWeek.Tuesday, new TimeOnly(10, 0), new TimeOnly(11, 0), 1);
        await factory.InsertReservationAsync(user.UserId, slot.Id, Today, ReservationStatus.Cancelled);
        await factory.InsertReservationAsync(user.UserId, slot.Id, Today, ReservationStatus.Attended);
        await Reservation(client, slot.Id, Today);
        await Expect(await Reserve(client, slot.Id, Today), HttpStatusCode.BadRequest, "No quedan cupos disponibles para este bloque.");
        await Reservation(client, slot.Id, Today.AddDays(7));
        foreach (var date in new[] { Today, Today.AddDays(7) })
        {
            var schedules = await Expect(await client.GetAsync($"/api/v1/schedules?date={date:yyyy-MM-dd}", Ct), HttpStatusCode.OK);
            var own = Assert.Single(schedules.EnumerateArray(), s => s.GetProperty("id").GetInt32() == slot.Id);
            Assert.Equal(1, own.GetProperty("reservedCount").GetInt32());
        }
    }

    [Fact]
    public async Task Get_OrdersByDateThenStartDescending_ForOwnerAndAdmin()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        using var admin = await factory.CreateAdminClientAsync();
        var early = await factory.CreateSlotAsync(DayOfWeek.Tuesday, new TimeOnly(8, 0), new TimeOnly(9, 0));
        var late = await factory.CreateSlotAsync(DayOfWeek.Tuesday, new TimeOnly(10, 0), new TimeOnly(11, 0));
        var older = await factory.InsertReservationAsync(user.UserId, late.Id, Today);
        var newerEarly = await factory.InsertReservationAsync(user.UserId, early.Id, Today.AddDays(7));
        var newerLate = await factory.InsertReservationAsync(user.UserId, late.Id, Today.AddDays(7));
        var expected = new[] { newerLate.Id, newerEarly.Id, older.Id };
        foreach (var (actor, path) in new[]
        {
            (client, $"/api/v1/reservations/user/{user.UserId}"),
            (admin, $"/api/v1/reservations/user/{user.UserId}"),
            (admin, "/api/v1/reservations"),
        })
        {
            var result = await Expect(await actor.GetAsync(path, Ct), HttpStatusCode.OK);
            var ids = result.EnumerateArray().Select(r => r.GetProperty("id").GetInt32())
                .Where(expected.Contains).ToArray();
            Assert.Equal(expected, ids);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-7)]
    public async Task Cancel_StartedOrPast_ClientRejectedAndAdminAllowed(int days)
    {
        var user = await factory.CreateClientUserAsync();
        using var real = user.Client;
        using var realAdmin = await factory.CreateAdminClientAsync();
        using var host = factory.WithClock(Clock());
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = real.DefaultRequestHeaders.Authorization;
        using var admin = host.CreateClient();
        admin.DefaultRequestHeaders.Authorization = realAdmin.DefaultRequestHeaders.Authorization;
        var slot = await factory.CreateSlotAsync(DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(10, 0));
        var reservation = await factory.InsertReservationAsync(user.UserId, slot.Id, Today.AddDays(days));
        await Expect(await client.DeleteAsync($"/api/v1/reservations/{reservation.Id}", Ct), HttpStatusCode.BadRequest,
            "La reserva ya no se puede cancelar porque el bloque comenzó.");
        var cancelled = await Expect(await admin.DeleteAsync($"/api/v1/reservations/{reservation.Id}", Ct), HttpStatusCode.OK, "Reserva cancelada correctamente.");
        Assert.Equal("Cancelado", cancelled.GetProperty("reservation").GetProperty("status").GetString());
    }

    [Theory]
    [InlineData(ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.Attended)]
    public async Task Cancel_Inactive_RejectedForOwnerAndAdmin(ReservationStatus status)
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        using var admin = await factory.CreateAdminClientAsync();
        var date = GymTime.Tomorrow();
        var slot = await factory.CreateSlotAsync(date.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(10, 0));
        var reservation = await factory.InsertReservationAsync(user.UserId, slot.Id, date, status);
        foreach (var actor in new[] { client, admin })
            await Expect(await actor.DeleteAsync($"/api/v1/reservations/{reservation.Id}", Ct), HttpStatusCode.BadRequest, "Solo se pueden cancelar reservas activas.");
    }
}
