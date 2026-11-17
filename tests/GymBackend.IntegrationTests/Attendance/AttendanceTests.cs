using System.Globalization;
using System.Net;
using GymBackend.IntegrationTests.Infrastructure;
using GymBackend.Services;
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
        await Expect(await Api.Attendance(admin, action, QrTokenService.GenerateToken(user.Rut, "otra-clave", DateTime.Now.Date)), HttpStatusCode.BadRequest, "Código QR inválido.");
        await Expect(await Api.Attendance(admin, action, QrTokenService.GenerateToken(user.Rut, factory.JwtKey, DateTime.Now.Date.AddDays(-1))), HttpStatusCode.BadRequest, "El código QR no corresponde al día de hoy.");
        await Expect(await Api.Attendance(admin, action, QrTokenService.GenerateToken(TestData.NextRut(), factory.JwtKey, DateTime.Now.Date)), HttpStatusCode.BadRequest, "Código QR inválido.");
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
        var now = DateTime.Now;
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
        using var client = user.Client;
        using var admin = await factory.CreateAdminClientAsync();
        var now = DateTime.Now;
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
