using System.Net;
using System.Net.Http.Json;
using GymBackend.Domain;
using GymBackend.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Reservations;

public sealed class DailyBookingTests(GymApiFactory factory)
{
    private const string Limit = "Los estudiantes pueden reservar como máximo una sesión por día.";

    [Fact]
    public async Task Student_OnlyOneActiveBooking_CancellationReleasesDay()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        var date = GymTime.Tomorrow();
        var first = await factory.CreateSlotAsync(date.DayOfWeek, new TimeOnly(8, 0), new TimeOnly(9, 0));
        var second = await factory.CreateSlotAsync(date.DayOfWeek, new TimeOnly(10, 0), new TimeOnly(11, 0));
        var reservation = await Reservation(client, first.Id, date);
        Assert.Equal(UserCategories.Estudiante, reservation.GetProperty("category").GetString());
        await Expect(await Reserve(client, second.Id, date), HttpStatusCode.Conflict, Limit);
        await Expect(await client.DeleteAsync($"/api/v1/reservations/{reservation.GetProperty("id").GetInt32()}", Ct), HttpStatusCode.OK);
        var booked = await Reservation(client, second.Id, date);
        await factory.WithDbAsync(async db =>
        {
            (await db.Reservations.SingleAsync(r => r.Id == booked.GetProperty("id").GetInt32(), Ct)).Status = ReservationStatus.Attended;
            await db.SaveChangesAsync(Ct);
        });
        await Expect(await Reserve(client, first.Id, date), HttpStatusCode.Conflict, Limit);
        var nextDate = date.AddDays(1);
        var nextSlot = await factory.CreateSlotAsync(nextDate.DayOfWeek, new TimeOnly(8, 0), new TimeOnly(9, 0));
        await Reservation(client, nextSlot.Id, nextDate);
    }

    [Theory]
    [InlineData(UserCategories.Deportista)]
    [InlineData(UserCategories.Funcionario)]
    [InlineData(UserCategories.Externo)]
    public async Task OtherCategories_HaveNoDailyLimit_AndSnapshotsSurviveIdentityEdits(string category)
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        using var admin = await factory.CreateAdminClientAsync();
        var branch = await Expect(await admin.PostAsJsonAsync("/api/v1/branches", new { name = $"Sport {Guid.NewGuid():N}" }, JsonOptions, Ct), HttpStatusCode.OK);
        var branchId = branch.GetProperty("branch").GetProperty("id").GetInt32();
        int? snapshotBranch = category == UserCategories.Deportista ? branchId : null;
        await Expect(await admin.PatchAsJsonAsync($"/api/v1/auth/users/{user.UserId}", new { category, sportBranchId = snapshotBranch }, JsonOptions, Ct), HttpStatusCode.OK);
        var date = GymTime.Tomorrow();
        var first = await factory.CreateSlotAsync(date.DayOfWeek, new TimeOnly(8, 0), new TimeOnly(9, 0));
        var second = await factory.CreateSlotAsync(date.DayOfWeek, new TimeOnly(10, 0), new TimeOnly(11, 0));
        var reservation = await Reservation(client, first.Id, date);
        await Reservation(client, second.Id, date);
        var another = await Expect(await admin.PostAsJsonAsync("/api/v1/branches", new { name = $"Sport {Guid.NewGuid():N}" }, JsonOptions, Ct), HttpStatusCode.OK);
        var anotherId = another.GetProperty("branch").GetProperty("id").GetInt32();
        await Expect(await admin.PatchAsJsonAsync($"/api/v1/auth/users/{user.UserId}", new { category = UserCategories.Deportista, sportBranchId = anotherId }, JsonOptions, Ct), HttpStatusCode.OK);
        var id = reservation.GetProperty("id").GetInt32();
        var stored = await factory.WithDbAsync(db => db.Reservations.SingleAsync(r => r.Id == id, Ct));
        Assert.Equal(category, stored.CategoryAtBooking);
        Assert.Equal(snapshotBranch, stored.SportBranchIdAtBooking);
        var listed = await Expect(await client.GetAsync($"/api/v1/reservations/user/{user.UserId}", Ct), HttpStatusCode.OK);
        var item = listed.EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == id);
        Assert.Equal(category, item.GetProperty("category").GetString());
        Assert.Equal(snapshotBranch, item.GetProperty("sportBranchId").ValueKind == System.Text.Json.JsonValueKind.Null ? (int?)null : item.GetProperty("sportBranchId").GetInt32());
    }

    [Fact]
    public async Task ConcurrentStudentBookings_InDifferentSlots_OnlyOneSucceeds()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        var date = GymTime.Tomorrow();
        var first = await factory.CreateSlotAsync(date.DayOfWeek, new TimeOnly(8, 0), new TimeOnly(9, 0));
        var second = await factory.CreateSlotAsync(date.DayOfWeek, new TimeOnly(10, 0), new TimeOnly(11, 0));
        var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> Send(int slot)
        {
            await start.Task.WaitAsync(Ct);
            return await Reserve(client, slot, date);
        }
        var tasks = new[] { Send(first.Id), Send(second.Id) };
        start.SetResult(true);
        var responses = await Task.WhenAll(tasks);
        try
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            var denied = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(Limit, (await Read(denied)).GetProperty("message").GetString());
            Assert.Equal(1, await factory.WithDbAsync(db => db.Reservations.CountAsync(r => r.UserId == user.UserId && r.Date == date, Ct)));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }
}
