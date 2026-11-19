using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GymBackend.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Time.Testing;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Contracts;

public sealed class ResponseContractTests(GymApiFactory factory)
{
    private const string UserShape = "id:n rut:s firstName:s lastName:s fullName:s email:s username:s category:s? sportBranch:o? role:s mustChangePassword:b enabled:b status:s createdAt:s updatedAt:s";
    private const string ReservationShape = "id:n userId:n userName:s scheduleSlotId:n date:s day:s startTime:s endTime:s zone:s capacity:n status:s createdAt:s category:s? sportBranchId:n?";
    private const string VisitShape = "id:n userId:n fullName:s rut:s reservationId:n? groupBookingId:n? date:s entryAt:s exitAt:s? stayMinutes:n? documentType:s entryOperator:s exitOperator:s?";

    [Fact]
    public async Task AuthResponses_HaveExactPropertiesAndTypes()
    {
        using var anonymous = factory.CreateClient();
        var login = await Expect(await anonymous.PostAsJsonAsync("/api/v1/auth/admin-login",
            new { username = factory.AdminUsername, password = factory.AdminPassword }, JsonOptions, Ct), HttpStatusCode.OK);
        Auth(login);
        using var admin = await factory.CreateAdminClientAsync();
        var rut = TestData.NextRut();
        var password = Guid.NewGuid().ToString("N");
        var registered = await Expect(await admin.PostAsJsonAsync("/api/v1/auth/register",
            new { rut, firstName = "Contrato", lastName = "Test", email = $"{rut}@example.test", password, role = "Usuario", category = "Estudiante" }, JsonOptions, Ct), HttpStatusCode.OK);
        UserMessage(registered);
        var id = registered.GetProperty("user").GetProperty("id").GetInt32();
        Auth(await Expect(await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { rut, password }, JsonOptions, Ct), HttpStatusCode.OK));
        var users = await Expect(await admin.GetAsync("/api/v1/auth/users", Ct), HttpStatusCode.OK);
        Array(users, UserShape);
        UserMessage(await Expect(await Status(admin, id, false), HttpStatusCode.OK));
        UserMessage(await Expect(await Status(admin, id, true), HttpStatusCode.OK));
        Shape(await Expect(await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { rut = "invalid", password }, JsonOptions, Ct), HttpStatusCode.BadRequest), "message:s");
        Shape(await Expect(await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { rut, password = "incorrect" }, JsonOptions, Ct), HttpStatusCode.Unauthorized), "message:s");
        Shape(await Expect(await Status(admin, -1, false), HttpStatusCode.NotFound), "message:s");
    }

    [Fact]
    public async Task ReservationsSchedulesQrAndAttendance_HaveExactPropertiesAndTypes()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 17, 10, 59, 0, TimeSpan.Zero));
        using var host = factory.WithClock(clock);
        var user = await factory.CreateClientUserAsync();
        using var realClient = user.Client;
        using var realAdmin = await factory.CreateAdminClientAsync();
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = realClient.DefaultRequestHeaders.Authorization;
        using var admin = host.CreateClient();
        admin.DefaultRequestHeaders.Authorization = realAdmin.DefaultRequestHeaders.Authorization;
        var slot = await factory.CreateSlotAsync(DayOfWeek.Tuesday, new TimeOnly(8, 0), new TimeOnly(9, 30));
        var created = await Expect(await Reserve(client, slot.Id, new DateOnly(2026, 11, 17)), HttpStatusCode.OK);
        ReservationMessage(created);
        var reservation = created.GetProperty("reservation");
        Assert.Equal(slot.Id, reservation.GetProperty("scheduleSlotId").GetInt32());
        Assert.Equal("2026-11-17", reservation.GetProperty("date").GetString());
        Assert.Equal("Martes", reservation.GetProperty("day").GetString());
        Assert.Equal("08:00", reservation.GetProperty("startTime").GetString());
        Assert.Equal("09:30", reservation.GetProperty("endTime").GetString());
        var id = created.GetProperty("reservation").GetProperty("id").GetInt32();
        Array(await Expect(await admin.GetAsync("/api/v1/reservations", Ct), HttpStatusCode.OK), ReservationShape);
        Array(await Expect(await client.GetAsync($"/api/v1/reservations/user/{user.UserId}", Ct), HttpStatusCode.OK), ReservationShape);
        Array(await Expect(await client.GetAsync("/api/v1/schedules", Ct), HttpStatusCode.OK), "id:n day:s startTime:s endTime:s zone:s capacity:n reservedCount:n date:s individualReservations:n groupSeats:n");
        var qr = await Expect(await client.GetAsync($"/api/v1/me/reservations/{id}/qr", Ct), HttpStatusCode.OK);
        Shape(qr, "token:s validFrom:s validUntil:s user:o session:o");
        var token = qr.GetProperty("token").GetString()!;
        var entry = await Expect(await Api.Access(admin, "entry", token), HttpStatusCode.OK);
        AttendanceMessage(entry);
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("visit").GetProperty("exitAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("visit").GetProperty("exitOperator").ValueKind);
        clock.Advance(TimeSpan.FromMinutes(1));
        var exit = await Expect(await Api.Exit(admin, user.UserId), HttpStatusCode.OK);
        AttendanceMessage(exit);
        Assert.Equal(JsonValueKind.String, exit.GetProperty("visit").GetProperty("exitAt").ValueKind);
        Assert.Equal(JsonValueKind.String, exit.GetProperty("visit").GetProperty("exitOperator").ValueKind);
        var futureSlot = await factory.CreateSlotAsync(DayOfWeek.Wednesday, new TimeOnly(8, 0), new TimeOnly(9, 30));
        var futureId = (await Reservation(client, futureSlot.Id, new DateOnly(2026, 11, 18))).GetProperty("id").GetInt32();
        ReservationMessage(await Expect(await client.DeleteAsync($"/api/v1/reservations/{futureId}", Ct), HttpStatusCode.OK));
        await Expect(await client.DeleteAsync($"/api/v1/reservations/{id}", Ct), HttpStatusCode.BadRequest, "Solo se pueden cancelar reservas activas.");
    }

    [Fact]
    public async Task MiddlewareUnauthorizedAndForbidden_RemainBodyless()
    {
        using var anonymous = factory.CreateClient();
        using var unauthorized = await anonymous.GetAsync("/api/v1/schedules", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Empty(await unauthorized.Content.ReadAsByteArrayAsync(Ct));
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        using var forbidden = await client.GetAsync("/api/v1/auth/users", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Empty(await forbidden.Content.ReadAsByteArrayAsync(Ct));
    }

    private static void Auth(JsonElement json)
    {
        Shape(json, "message:s user:o expiresAt:s");
        Shape(json.GetProperty("user"), UserShape);
    }

    private static void UserMessage(JsonElement json)
    {
        Shape(json, "message:s user:o");
        Shape(json.GetProperty("user"), UserShape);
    }

    private static void ReservationMessage(JsonElement json)
    {
        Shape(json, "message:s reservation:o");
        Shape(json.GetProperty("reservation"), ReservationShape);
    }

    private static void AttendanceMessage(JsonElement json)
    {
        Shape(json, "message:s visit:o");
        Shape(json.GetProperty("visit"), VisitShape);
    }

    private static void Array(JsonElement json, string shape)
    {
        Assert.Equal(JsonValueKind.Array, json.ValueKind);
        Assert.NotEmpty(json.EnumerateArray());
        foreach (var item in json.EnumerateArray()) Shape(item, shape);
    }

    private static void Shape(JsonElement json, string shape)
    {
        Assert.Equal(JsonValueKind.Object, json.ValueKind);
        var expected = shape.Split(' ').Select(field => field.Split(':')).ToArray();
        Assert.Equal(expected.Select(field => field[0]).OrderBy(name => name),
            json.EnumerateObject().Select(property => property.Name).OrderBy(name => name));
        foreach (var field in expected)
        {
            var kind = json.GetProperty(field[0]).ValueKind;
            var type = field[1];
            if (type.EndsWith('?') && kind == JsonValueKind.Null) continue;
            Assert.True(type[0] switch
            {
                's' => kind == JsonValueKind.String,
                'n' => kind == JsonValueKind.Number,
                'b' => kind is JsonValueKind.True or JsonValueKind.False,
                'o' => kind == JsonValueKind.Object,
                _ => false,
            }, $"Tipo inesperado para {field[0]}: {kind}");
        }
    }
}
