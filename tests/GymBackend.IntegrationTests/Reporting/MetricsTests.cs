using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using GymBackend.Domain;
using GymBackend.IntegrationTests.Infrastructure;
using GymBackend.Models;
using Microsoft.EntityFrameworkCore;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Reporting;

public sealed class MetricsTests(GymApiFactory factory)
{
    [Fact]
    public async Task EmptyPeriodHasZeroCountsAndNullAverage()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var metrics = await Expect(await admin.GetAsync("/api/v1/admin/metrics?from=1900-01-01&to=1900-01-02", Ct), HttpStatusCode.OK);
        Assert.Equal(0, metrics.GetProperty("reservations").GetProperty("total").GetInt32());
        Assert.Equal(0, metrics.GetProperty("visits").GetInt32());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, metrics.GetProperty("averageStayMinutes").ValueKind);
        Assert.Empty(metrics.GetProperty("demandByHour").EnumerateArray());
    }

    [Fact]
    public async Task BranchFilterUsesSnapshotAndOccupancyRemainsGlobal()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        var branch = await factory.WithDbAsync(async db =>
        {
            var b = new SportBranch { Name = "Metrics " + Guid.NewGuid().ToString("N"), CreatedAt = DateTimeOffset.UtcNow };
            db.SportBranches.Add(b);
            await db.SaveChangesAsync(Ct);
            return b;
        });
        var date = new DateOnly(2022, 2, 7);
        var slot = await factory.CreateSlotAsync(date.DayOfWeek, new(16, 0), new(17, 0));
        var reservation = await factory.InsertReservationAsync(user.UserId, slot.Id, date, ReservationStatus.Attended);
        await factory.WithDbAsync(async db =>
        {
            var stored = await db.Reservations.SingleAsync(r => r.Id == reservation.Id, Ct);
            stored.CategoryAtBooking = UserCategories.Deportista;
            stored.SportBranchIdAtBooking = branch.Id;
            db.Visits.Add(new Visit
            {
                UserId = user.UserId,
                ReservationId = reservation.Id,
                Date = date,
                EntryAt = new DateTime(2022, 2, 7, 19, 0, 0, DateTimeKind.Utc),
                ExitAt = new DateTime(2022, 2, 7, 19, 30, 0, DateTimeKind.Utc),
                EntryOperatorId = user.UserId,
                ExitOperatorId = user.UserId,
                DocumentType = "TNE"
            });
            await db.SaveChangesAsync(Ct);
        });
        var global = await Expect(await admin.GetAsync("/api/v1/admin/metrics?from=2022-02-07&to=2022-02-07", Ct), HttpStatusCode.OK);
        var metrics = await Expect(await admin.GetAsync($"/api/v1/admin/metrics?from=2022-02-07&to=2022-02-07&branchId={branch.Id}&category=Deportista", Ct), HttpStatusCode.OK);
        Assert.Equal(1, metrics.GetProperty("reservations").GetProperty("attended").GetInt32());
        Assert.Equal(1, metrics.GetProperty("uniqueVisitors").GetInt32());
        Assert.Equal(30, metrics.GetProperty("averageStayMinutes").GetDouble());
        Assert.Equal(global.GetProperty("occupancy").GetRawText(), metrics.GetProperty("occupancy").GetRawText());
        Assert.Equal(branch.Id, Assert.Single(metrics.GetProperty("demandByBranch").EnumerateArray()).GetProperty("branchId").GetInt32());
        using var export = await admin.PostAsJsonAsync("/api/v1/admin/exports", new
        {
            from = "2022-02-07",
            to = "2022-02-07",
            branchId = branch.Id,
            category = "Deportista",
        }, JsonOptions, Ct);
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        await using var stream = await export.Content.ReadAsStreamAsync(Ct);
        using var workbook = new XLWorkbook(stream);
        var exportedReservation = Assert.Single(workbook.Worksheet("Reservas").RowsUsed().Skip(1));
        Assert.Equal(reservation.Id, exportedReservation.Cell(1).GetValue<int>());
        Assert.Equal("Deportista", exportedReservation.Cell(8).GetString());
        Assert.Equal(branch.Name, exportedReservation.Cell(9).GetString());
        var receptionist = await factory.CreateReceptionistClientAsync();
        using var reception = receptionist.Client;
        using var denied = await reception.GetAsync("/api/v1/admin/metrics", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
}
