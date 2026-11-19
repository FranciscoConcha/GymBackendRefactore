using System.Net;
using GymBackend.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Reservations;

public sealed class CategoryWindowTests(GymApiFactory factory)
{
    [Theory]
    [InlineData("Estudiante", 0, 1, "CalendarDays", false)]
    [InlineData("Estudiante", 1, 1, "CalendarDays", true)]
    [InlineData("Funcionario", 3, 3, "CalendarDays", true)]
    [InlineData("Externo", 3, 4, "CalendarDays", false)]
    [InlineData("Estudiante", 1, 3, "BusinessDays", true)]
    [InlineData("Estudiante", 1, 4, "BusinessDays", false)]
    [InlineData("Deportista", 8, 8, "CalendarDays", true)]
    public async Task Booking_UsesDatabaseCategoryAndSameHorizonUnit(string category, int categoryDays, int offset, string unit, bool allowed)
    {
        // Friday: one business day reaches Monday.
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero));
        using var host = factory.WithClockAndSettings(clock, new Dictionary<string, string?>
        {
            ["Reservations:MaxDaysAhead"] = "2",
            ["Reservations:HorizonUnit"] = unit,
            [$"Reservations:CategoryMaxDaysAhead:{category}"] = categoryDays.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });
        var user = await factory.CreateClientUserAsync();
        using var existing = user.Client;
        await factory.WithDbAsync(async db =>
        {
            var entity = await db.Users.SingleAsync(u => u.Id == user.UserId, Ct);
            entity.Category = category;
            if (category == "Deportista")
            {
                entity.SportBranch = new GymBackend.Models.SportBranch { Name = $"Window {Guid.NewGuid():N}", Enabled = true };
            }
            await db.SaveChangesAsync(Ct);
        });
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = existing.DefaultRequestHeaders.Authorization;
        var date = new DateOnly(2026, 11, 20).AddDays(offset);
        var slot = await factory.CreateSlotAsync(date.DayOfWeek, new TimeOnly(10, 0), new TimeOnly(11, 0));
        await Expect(await Reserve(client, slot.Id, date), allowed ? HttpStatusCode.OK : HttpStatusCode.BadRequest,
            allowed ? "Reserva creada correctamente." : "La fecha excede el plazo máximo de reserva.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingCategoryOrStaff_UsesGlobalFallback(bool staff)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero));
        using var host = factory.WithClockAndSettings(clock, new Dictionary<string, string?>
        {
            ["Reservations:MaxDaysAhead"] = "2",
            ["Reservations:CategoryMaxDaysAhead:Deportista"] = "10",
        });
        var user = staff ? await factory.CreateDedicatedAdminClientAsync() : default;
        var student = staff ? default : await factory.CreateClientUserAsync();
        using var existing = staff ? user.Client : student.Client;
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = existing.DefaultRequestHeaders.Authorization;
        foreach (var days in new[] { 2, 3 })
        {
            var date = new DateOnly(2026, 11, 20).AddDays(days);
            var slot = await factory.CreateSlotAsync(date.DayOfWeek, new TimeOnly(10, 0), new TimeOnly(11, 0));
            await Expect(await Reserve(client, slot.Id, date), days == 2 ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
        }
    }

    [Fact]
    public async Task NoShow_PersistsAsInasistencia()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        var slot = await factory.CreateSlotAsync(DayOfWeek.Friday, new TimeOnly(10, 0), new TimeOnly(11, 0));
        var reservation = await factory.InsertReservationAsync(user.UserId, slot.Id, new DateOnly(2026, 11, 20), GymBackend.Domain.ReservationStatus.NoShow);
        var status = await factory.WithDbAsync(db => db.Reservations.Where(r => r.Id == reservation.Id).Select(r => r.Status).SingleAsync(Ct));
        Assert.Equal("Inasistencia", GymBackend.Domain.ReservationStatuses.Name(status));
    }
}
