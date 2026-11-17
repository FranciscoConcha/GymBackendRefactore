using System.Globalization;
using Microsoft.Extensions.Time.Testing;
using System.Net;
using GymBackend.IntegrationTests.Infrastructure;
using GymBackend.Application.Qr;
using Microsoft.EntityFrameworkCore;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Attendance;

public sealed class AttendanceTests(GymApiFactory factory)
{
    [Theory]
    [InlineData("entry")]
    [InlineData("exit")]
    public async Task Action_PermisosYTokensInvalidos_Rechazado(string action)
    {
        using var anonymous = factory.CreateClient();
        using var denied = await Api.Attendance(anonymous, action, "basura");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        using var forbidden = await Api.Attendance(client, action, "basura");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var admin = await factory.CreateAdminClientAsync();
        await Expect(await Api.Attendance(admin, action, "basura"), HttpStatusCode.BadRequest, "Código QR inválido.");
        await Expect(await Api.Attendance(admin, action, QrTokenFormat.Create(user.Rut, "otra-clave", DateOnly.FromDateTime(GymTime.Today()))), HttpStatusCode.BadRequest, "Código QR inválido.");
        await Expect(await Api.Attendance(admin, action, QrTokenFormat.Create(user.Rut, factory.JwtKey, DateOnly.FromDateTime(GymTime.Today().AddDays(-1)))), HttpStatusCode.BadRequest, "El código QR no corresponde al día de hoy.");
        await Expect(await Api.Attendance(admin, action, QrTokenFormat.Create(TestData.NextRut(), factory.JwtKey, DateOnly.FromDateTime(GymTime.Today()))), HttpStatusCode.BadRequest, "Código QR inválido.");
        await Expect(await Status(admin, user.UserId, false), HttpStatusCode.OK);
        await Expect(await Api.Attendance(admin, action, await Api.Qr(client)), HttpStatusCode.BadRequest, "El usuario no está habilitado.");
    }

    [Fact]
    public async Task Entry_SinReservaEnVentana_BadRequest()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        using var admin = await factory.CreateAdminClientAsync();
        await Expect(await Api.Attendance(admin, "entry", await Api.Qr(client)), HttpStatusCode.BadRequest, "El usuario no se encuentra registrado en el horario de ingreso.");
        // También fija el rechazo cuando existe una reserva, pero está fuera de la ventana.
        var now = GymTime.Now().DateTime;
        var start = now.Hour < 12 ? "20:00" : "02:00";
        var end = now.Hour < 12 ? "21:00" : "03:00";
        var day = new[] { "Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado" }[(int)now.DayOfWeek];
        await Reservation(client, await factory.CreateSlotAsync(day, start, end));
        await Expect(await Api.Attendance(admin, "entry", await Api.Qr(client)), HttpStatusCode.BadRequest, "El usuario no se encuentra registrado en el horario de ingreso.");
    }

    [Fact]
    public async Task Exit_SinEntrada_BadRequest()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        using var admin = await factory.CreateAdminClientAsync();
        await Expect(await Api.Attendance(admin, "exit", await Api.Qr(client)), HttpStatusCode.BadRequest, "Usuario no registra una entrada en el día.");
    }

    [Fact]
    public async Task EntryExit_ReservaActual_PermiteFlujoYReingreso()
    {
        var user = await factory.CreateClientUserAsync();
        using var realClient = user.Client;
        using var realAdmin = await factory.CreateAdminClientAsync();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 17, 13, 0, 0, TimeSpan.Zero));
        using var host = factory.WithClock(clock);
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = realClient.DefaultRequestHeaders.Authorization;
        using var admin = host.CreateClient();
        admin.DefaultRequestHeaders.Authorization = realAdmin.DefaultRequestHeaders.Authorization;
        var now = new DateTime(2026, 11, 17, 10, 0, 0);
        var day = new[] { "Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado" }[(int)now.DayOfWeek];
        var start = now.AddMinutes(-10) < now.Date ? now.Date : now.AddMinutes(-10);
        var end = now.AddMinutes(60) >= now.Date.AddDays(1) ? now.Date.AddHours(23).AddMinutes(59) : now.AddMinutes(60);
        var slot = await factory.CreateSlotAsync(day, start.ToString("HH:mm", CultureInfo.InvariantCulture), end.ToString("HH:mm", CultureInfo.InvariantCulture));
        var reservationId = (await Reservation(client, slot)).GetProperty("id").GetInt32();
        var token = await Api.Qr(client);
        var entry = await Expect(await Api.Attendance(admin, "entry", token), HttpStatusCode.OK, "Entrada registrada correctamente.");
        var attendanceId = entry.GetProperty("attendance").GetProperty("id").GetInt32();
        await factory.WithDbAsync(async db =>
        {
            var attendance = await db.Attendances.SingleAsync(a => a.Id == attendanceId, Ct);
            Assert.True(attendance.CheckedIn);
            Assert.Equal(user.UserId, attendance.UserId);
            Assert.Equal(reservationId, attendance.ReservationId);
            Assert.Equal("Asistió", (await db.Reservations.SingleAsync(r => r.Id == reservationId, Ct)).Status);
        });
        await Expect(await Api.Attendance(admin, "entry", token), HttpStatusCode.BadRequest, "Usuario ya registra una entrada en el día.");
        var exit = await Expect(await Api.Attendance(admin, "exit", token), HttpStatusCode.OK, "Salida registrada correctamente.");
        Assert.False(exit.GetProperty("attendance").GetProperty("checkedIn").GetBoolean());
        await factory.WithDbAsync(async db =>
        {
            var attendance = await db.Attendances.SingleAsync(a => a.Id == attendanceId, Ct);
            Assert.False(attendance.CheckedIn);
            Assert.NotNull(attendance.ExitTime);
        });
        await Expect(await Api.Attendance(admin, "exit", token), HttpStatusCode.BadRequest, "Usuario no registra una entrada en el día.");
        var reentry = await Expect(await Api.Attendance(admin, "entry", token), HttpStatusCode.OK, "Entrada registrada correctamente.");
        Assert.True(reentry.GetProperty("attendance").GetProperty("checkedIn").GetBoolean());
        Assert.Equal(attendanceId, reentry.GetProperty("attendance").GetProperty("id").GetInt32());
    }
}
