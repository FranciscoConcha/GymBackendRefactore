using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GymBackend.IntegrationTests.Infrastructure;
using GymBackend.Security;
using Microsoft.EntityFrameworkCore;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Security;

public sealed class CookieMutationTests(GymApiFactory factory)
{
    [Fact]
    public async Task ReservationMutation_RequiresCookieCsrfAndAllowedOrigin()
    {
        var user = await factory.CreateClientUserAsync();
        using var userWithBearer = user.Client;
        var date = GymTime.Tomorrow();
        var slot = await factory.CreateSlotAsync(date.DayOfWeek, new TimeOnly(8, 0), new TimeOnly(9, 0));
        using var client = factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { rut = user.Rut, password = user.Password }, JsonOptions, Ct);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Null(client.DefaultRequestHeaders.Authorization);

        client.DefaultRequestHeaders.Remove(CsrfProtectionMiddleware.HeaderName);
        using (var missingHeader = await Reserve(client, slot.Id, date))
            Assert.Equal(HttpStatusCode.Forbidden, missingHeader.StatusCode);
        Assert.Equal(0, await ReservationCount(slot.Id, date));

        client.DefaultRequestHeaders.Add(CsrfProtectionMiddleware.HeaderName, CsrfProtectionMiddleware.HeaderValue);
        client.DefaultRequestHeaders.Add("Origin", "https://evil.example");
        using (var evilOrigin = await Reserve(client, slot.Id, date))
            Assert.Equal(HttpStatusCode.Forbidden, evilOrigin.StatusCode);
        Assert.Equal(0, await ReservationCount(slot.Id, date));

        client.DefaultRequestHeaders.Remove("Origin");
        using var accepted = await Reserve(client, slot.Id, date);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(1, await factory.WithDbAsync(db => db.Reservations.CountAsync(
            reservation => reservation.UserId == user.UserId && reservation.ScheduleSlotId == slot.Id && reservation.Date == date, Ct)));
    }

    [Theory]
    [InlineData("Bearer basura")]
    [InlineData("Basic dXNlcjpwYXNz")]
    public async Task InvalidAuthorizationHeader_DoesNotFallBackToValidCookie(string authorization)
    {
        var user = await factory.CreateClientUserAsync();
        using var userWithBearer = user.Client;
        using var client = await LoginAsClient(user.Rut, user.Password);
        using (var withCookie = await client.GetAsync("/api/v1/auth/me", Ct))
            Assert.Equal(HttpStatusCode.OK, withCookie.StatusCode);
        client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(authorization);

        using var response = await client.GetAsync("/api/v1/auth/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminStatusMutation_UsesCookieAndRequiresCsrf()
    {
        var user = await factory.CreateClientUserAsync();
        using var userWithBearer = user.Client;
        using var admin = factory.CreateClient();
        using var login = await admin.PostAsJsonAsync("/api/v1/auth/admin-login",
            new { username = factory.AdminUsername, password = factory.AdminPassword }, JsonOptions, Ct);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Null(admin.DefaultRequestHeaders.Authorization);

        using var accepted = await Status(admin, user.UserId, false);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.False(await UserEnabled(user.UserId));

        admin.DefaultRequestHeaders.Remove(CsrfProtectionMiddleware.HeaderName);
        using var rejected = await Status(admin, user.UserId, true);
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        Assert.False(await UserEnabled(user.UserId));
    }

    private async Task<HttpClient> LoginAsClient(string rut, string password)
    {
        var client = factory.CreateClient();
        try
        {
            using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { rut, password }, JsonOptions, Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Null(client.DefaultRequestHeaders.Authorization);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private Task<int> ReservationCount(int slotId, DateOnly date) =>
        factory.WithDbAsync(db => db.Reservations.CountAsync(
            reservation => reservation.ScheduleSlotId == slotId && reservation.Date == date, Ct));

    private Task<bool> UserEnabled(int userId) =>
        factory.WithDbAsync(db => db.Users.Where(user => user.Id == userId).Select(user => user.Enabled).SingleAsync(Ct));
}
