using System.Globalization;
using System.Net;
using GymBackend.Application.Qr;
using GymBackend.Domain;
using GymBackend.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Attendance;

public sealed class AttendanceTests(GymApiFactory factory)
{
    [Theory]
    [InlineData("entry", "El usuario no está habilitado.")]
    [InlineData("exit", "Usuario no registra una entrada en el día.")]
    public async Task Action_PermisosYTokensInvalidos_Rechazado(string action, string disabledError)
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
        await Expect(await Api.Attendance(admin, action, QrTokenFormat.Create(user.Rut, factory.QrKey, DateOnly.FromDateTime(GymTime.Today().AddDays(-1)))), HttpStatusCode.BadRequest, "El código QR no corresponde al día de hoy.");
        await Expect(await Api.Attendance(admin, action, QrTokenFormat.Create(TestData.NextRut(), factory.QrKey, DateOnly.FromDateTime(GymTime.Today()))), HttpStatusCode.BadRequest, "Código QR inválido.");
        var qrBeforeDisabling = await Api.Qr(client);
        await Expect(await Status(admin, user.UserId, false), HttpStatusCode.OK);
        await Expect(await Api.Attendance(admin, action, qrBeforeDisabling), HttpStatusCode.BadRequest, disabledError);
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
        var day = now.DayOfWeek;
        var slot = await factory.CreateSlotAsync(day, TimeOnly.Parse(start, CultureInfo.InvariantCulture), TimeOnly.Parse(end, CultureInfo.InvariantCulture));
        await factory.InsertReservationAsync(user.UserId, slot.Id, DateOnly.FromDateTime(now));
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
        var day = now.DayOfWeek;
        var start = now.AddMinutes(-10) < now.Date ? now.Date : now.AddMinutes(-10);
        var end = now.AddMinutes(60) >= now.Date.AddDays(1) ? now.Date.AddHours(23).AddMinutes(59) : now.AddMinutes(60);
        var slot = await factory.CreateSlotAsync(day, TimeOnly.FromDateTime(start), TimeOnly.FromDateTime(end));
        var reservationId = (await factory.InsertReservationAsync(user.UserId, slot.Id, new DateOnly(2026, 11, 17))).Id;
        var token = await Api.Qr(client);
        var entry = await Expect(await Api.Attendance(admin, "entry", token), HttpStatusCode.OK, "Entrada registrada correctamente.");
        var attendanceId = entry.GetProperty("attendance").GetProperty("id").GetInt32();
        await factory.WithDbAsync(async db =>
        {
            var attendance = await db.Attendances.SingleAsync(a => a.Id == attendanceId, Ct);
            Assert.True(attendance.CheckedIn);
            Assert.Equal(user.UserId, attendance.UserId);
            Assert.Equal(reservationId, attendance.ReservationId);
            Assert.Equal(ReservationStatus.Attended, (await db.Reservations.SingleAsync(r => r.Id == reservationId, Ct)).Status);
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


    [Fact]
    public async Task Exit_DeshabilitadoConEntradaAbierta_Permitido()
    {
        var user = await factory.CreateClientUserAsync();
        using var realClient = user.Client;
        using var realAdmin = await factory.CreateAdminClientAsync();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 17, 13, 0, 0, TimeSpan.Zero));
        using var host = factory.WithClock(clock);
        using var client = host.CreateClient(); client.DefaultRequestHeaders.Authorization = realClient.DefaultRequestHeaders.Authorization;
        using var admin = host.CreateClient(); admin.DefaultRequestHeaders.Authorization = realAdmin.DefaultRequestHeaders.Authorization;
        var slot = await factory.CreateSlotAsync(new DateTime(2026, 11, 17).DayOfWeek, new TimeOnly(9, 0), new TimeOnly(11, 0));
        await factory.InsertReservationAsync(user.UserId, slot.Id, new DateOnly(2026, 11, 17));
        var token = await Api.Qr(client);
        await Expect(await Api.Attendance(admin, "entry", token), HttpStatusCode.OK, "Entrada registrada correctamente.");
        await Expect(await Status(admin, user.UserId, false), HttpStatusCode.OK);
        var exit = await Expect(await Api.Attendance(admin, "exit", token), HttpStatusCode.OK, "Salida registrada correctamente.");
        Assert.False(exit.GetProperty("attendance").GetProperty("checkedIn").GetBoolean());
        Assert.NotNull(exit.GetProperty("attendance").GetProperty("exitTime").GetString());
    }

    [Fact]
    public async Task Exit_DespuesDelFinDelBloque_Permitido()
    {
        var user = await factory.CreateClientUserAsync();
        using var realClient = user.Client; using var realAdmin = await factory.CreateAdminClientAsync();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 17, 13, 0, 0, TimeSpan.Zero));
        using var host = factory.WithClock(clock);
        using var client = host.CreateClient(); client.DefaultRequestHeaders.Authorization = realClient.DefaultRequestHeaders.Authorization;
        using var admin = host.CreateClient(); admin.DefaultRequestHeaders.Authorization = realAdmin.DefaultRequestHeaders.Authorization;
        var slot = await factory.CreateSlotAsync(new DateTime(2026, 11, 17).DayOfWeek, new TimeOnly(9, 0), new TimeOnly(11, 0));
        await factory.InsertReservationAsync(user.UserId, slot.Id, new DateOnly(2026, 11, 17)); var token = await Api.Qr(client);
        await Expect(await Api.Attendance(admin, "entry", token), HttpStatusCode.OK, "Entrada registrada correctamente.");
        clock.Advance(TimeSpan.FromHours(2).Add(TimeSpan.FromSeconds(1)));
        await Expect(await Api.Attendance(admin, "exit", token), HttpStatusCode.OK, "Salida registrada correctamente.");
    }

    [Fact]
    public async Task Entry_FinExclusivoYReentryDespuesDelFin()
    {
        var users = new[] { await factory.CreateClientUserAsync(), await factory.CreateClientUserAsync(), await factory.CreateClientUserAsync() };
        using var realAdmin = await factory.CreateAdminClientAsync();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 17, 12, 59, 59, TimeSpan.Zero));
        using var host = factory.WithClock(clock); using var admin = host.CreateClient(); admin.DefaultRequestHeaders.Authorization = realAdmin.DefaultRequestHeaders.Authorization;
        var day = new DateTime(2026, 11, 17).DayOfWeek; var date = new DateOnly(2026, 11, 17);
        var endSlot = await factory.CreateSlotAsync(day, new TimeOnly(9, 0), new TimeOnly(10, 0));
        var nextSlot = await factory.CreateSlotAsync(day, new TimeOnly(9, 0), new TimeOnly(10, 1));
        await factory.InsertReservationAsync(users[0].UserId, endSlot.Id, date);
        await factory.InsertReservationAsync(users[1].UserId, nextSlot.Id, date);
        await factory.InsertReservationAsync(users[2].UserId, endSlot.Id, date);
        using var c0 = host.CreateClient(); c0.DefaultRequestHeaders.Authorization = users[0].Client.DefaultRequestHeaders.Authorization;
        using var c1 = host.CreateClient(); c1.DefaultRequestHeaders.Authorization = users[1].Client.DefaultRequestHeaders.Authorization;
        using var c2 = host.CreateClient(); c2.DefaultRequestHeaders.Authorization = users[2].Client.DefaultRequestHeaders.Authorization;
        var t0 = await Api.Qr(c0); var t1 = await Api.Qr(c1); var t2 = await Api.Qr(c2);
        await Expect(await Api.Attendance(admin, "entry", t0), HttpStatusCode.OK, "Entrada registrada correctamente.");
        await Expect(await Api.Attendance(admin, "entry", t1), HttpStatusCode.OK, "Entrada registrada correctamente.");
        clock.Advance(TimeSpan.FromSeconds(1));
        await Expect(await Api.Attendance(admin, "entry", t2), HttpStatusCode.BadRequest, "El usuario no se encuentra registrado en el horario de ingreso.");
        await Expect(await Api.Attendance(admin, "exit", t0), HttpStatusCode.OK, "Salida registrada correctamente.");
        clock.Advance(TimeSpan.FromSeconds(1));
        await Expect(await Api.Attendance(admin, "entry", t0), HttpStatusCode.BadRequest, "El usuario no se encuentra registrado en el horario de ingreso.");
    }
}
