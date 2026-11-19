using System.Net;
using System.Net.Http.Json;
using GymBackend.Domain;
using GymBackend.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GymBackend.IntegrationTests.Concurrency;

public sealed class ReservationConcurrencyTests(GymApiFactory factory)
{
    [Fact]
    public async Task Capacity_IsNeverExceeded()
    {
        for (var iteration = 0; iteration < 3; iteration++)
        {
            var date = GymTime.Tomorrow();
            var slot = await factory.CreateSlotAsync(date.DayOfWeek, new TimeOnly(10, 0), new TimeOnly(11, 0), 3);
            var clients = new List<HttpClient>();
            try
            {
                for (var i = 0; i < 12; i++)
                    clients.Add((await factory.CreateClientUserAsync()).Client);
                var responses = await Together(clients, client => Api.Reserve(client, slot.Id, date));
                try
                {
                    Assert.Equal(3, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
                    foreach (var response in responses.Where(r => r.StatusCode != HttpStatusCode.OK))
                    {
                        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                        Assert.Equal("No quedan cupos disponibles para este bloque.",
                            (await Api.Read(response)).GetProperty("message").GetString());
                    }
                    Assert.Equal(3, await factory.WithDbAsync(db => db.Reservations.CountAsync(r =>
                        r.ScheduleSlotId == slot.Id && r.Date == date && r.Status == ReservationStatus.Reserved, Api.Ct)));
                }
                finally { foreach (var response in responses) response.Dispose(); }
            }
            finally { foreach (var client in clients) client.Dispose(); }
        }
    }

    [Fact]
    public async Task SameUser_OnlyCreatesOneActiveReservation()
    {
        var user = await factory.CreateClientUserAsync();
        using var originalClient = user.Client;
        var date = GymTime.Tomorrow();
        var slot = await factory.CreateSlotAsync(date.DayOfWeek, new TimeOnly(12, 0), new TimeOnly(13, 0));
        var clients = Enumerable.Range(0, 8).Select(_ => factory.CreateClientWithToken(
            factory.CreateJwt(user.UserId, Roles.Usuario, user.Rut))).ToArray();
        try
        {
            var responses = await Together(clients, client => Api.Reserve(client, slot.Id, date));
            try
            {
                Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
                Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest or HttpStatusCode.Conflict));
                Assert.Equal(1, await factory.WithDbAsync(db => db.Reservations.CountAsync(r =>
                    r.UserId == user.UserId && r.ScheduleSlotId == slot.Id && r.Date == date &&
                    r.Status == ReservationStatus.Reserved, Api.Ct)));
            }
            finally { foreach (var response in responses) response.Dispose(); }
        }
        finally { foreach (var client in clients) client.Dispose(); }
    }

    [Fact]
    public async Task SameRut_OnlyRegistersOneUser()
    {
        var rut = TestData.NextRut();
        var clients = new List<HttpClient>();
        try
        {
            for (var i = 0; i < 6; i++) clients.Add(await factory.CreateAdminClientAsync());
            var responses = await Together(clients, client => client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                rut,
                firstName = "Concurrent",
                lastName = "Registration",
                email = $"{Guid.NewGuid():N}@example.test",
                password = "Concurrent-Test-123!",
                role = "Usuario",
                category = "Estudiante",
            }, Api.JsonOptions, Api.Ct));
            try
            {
                Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
                Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest or HttpStatusCode.Conflict));
                Assert.Equal(1, await factory.WithDbAsync(db => db.Users.CountAsync(u => u.Rut == rut, Api.Ct)));
            }
            finally { foreach (var response in responses) response.Dispose(); }
        }
        finally { foreach (var client in clients) client.Dispose(); }
    }

    private static async Task<HttpResponseMessage[]> Together(IEnumerable<HttpClient> clients,
        Func<HttpClient, Task<HttpResponseMessage>> send)
    {
        var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = clients.Select(async client =>
        {
            await start.Task.WaitAsync(Api.Ct);
            return await send(client);
        }).ToArray();
        start.SetResult(true);
        return await Task.WhenAll(tasks);
    }
}
