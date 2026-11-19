using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using GymBackend.Domain;
using GymBackend.IntegrationTests.Infrastructure;
using GymBackend.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Auth;

public sealed class TemporaryPasswordTests(GymApiFactory factory)
{
    private const string Initial = "Temporary123!";
    private const string Next = "NewPassword123!";
    private const string Pending = "Debe cambiar su contraseña temporal antes de continuar.";

    private async Task<(int Id, string Rut)> Register(HttpClient admin)
    {
        var rut = TestData.NextRut();
        var result = await Expect(await admin.PostAsJsonAsync("/api/v1/auth/register", new
        {
            rut,
            firstName = "Password",
            lastName = "Test",
            email = $"{rut}@example.test",
            password = Initial,
            role = Roles.Usuario,
            category = UserCategories.Estudiante,
        }, JsonOptions, Ct), HttpStatusCode.OK);
        Assert.True(result.GetProperty("user").GetProperty("mustChangePassword").GetBoolean());
        return (result.GetProperty("user").GetProperty("id").GetInt32(), rut);
    }

    private static string Token(HttpResponseMessage response) => response.Headers.GetValues("Set-Cookie")
        .Single(v => v.StartsWith(SessionCookie.Name + "=", StringComparison.Ordinal)).Split(';')[0].Split('=', 2)[1];

    private async Task AssertRevoked(string token)
    {
        using var bearer = factory.CreateClientWithToken(token);
        using var bearerResponse = await bearer.GetAsync("/api/v1/auth/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, bearerResponse.StatusCode);
        using var cookie = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        cookie.DefaultRequestHeaders.Add("Cookie", SessionCookie.Name + "=" + token);
        using var cookieResponse = await cookie.GetAsync("/api/v1/auth/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, cookieResponse.StatusCode);
    }

    [Fact]
    public async Task FirstLogin_RequiresChange_AndLogoutRevokesPendingToken()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var user = await Register(admin);
        using var client = factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { rut = user.Rut, password = Initial }, JsonOptions, Ct);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.True((await Read(login)).GetProperty("user").GetProperty("mustChangePassword").GetBoolean());
        await Expect(await client.GetAsync("/api/v1/schedules", Ct), HttpStatusCode.Forbidden, Pending);
        await Expect(await client.GetAsync("/api/v1/auth/me", Ct), HttpStatusCode.OK);
        await Expect(await client.PostAsync("/api/v1/auth/logout", null, Ct), HttpStatusCode.OK);
        await AssertRevoked(Token(login));
        await Expect(await client.PostAsync("/api/v1/auth/logout", null, Ct), HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_Validates_ReissuesCookie_RevokesAndAudits()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var user = await Register(admin);
        using var client = factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { rut = user.Rut, password = Initial }, JsonOptions, Ct);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await Expect(await client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = "wrong", newPassword = Next }, JsonOptions, Ct),
            HttpStatusCode.BadRequest, "La contraseña actual no es correcta.");
        foreach (var invalid in new[] { "short", new string('a', 129), Initial })
            await Expect(await client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = Initial, newPassword = invalid }, JsonOptions, Ct), HttpStatusCode.BadRequest);
        using var changed = await client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = Initial, newPassword = Next }, JsonOptions, Ct);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.NotEqual(Token(login), Token(changed));
        Assert.False((await Read(changed)).GetProperty("user").GetProperty("mustChangePassword").GetBoolean());
        await Expect(await client.GetAsync("/api/v1/schedules", Ct), HttpStatusCode.OK);
        await AssertRevoked(Token(login));
        await Expect(await client.PostAsJsonAsync("/api/v1/auth/login", new { rut = user.Rut, password = Next }, JsonOptions, Ct), HttpStatusCode.OK);
        await Expect(await client.PostAsJsonAsync("/api/v1/auth/login", new { rut = user.Rut, password = Initial }, JsonOptions, Ct), HttpStatusCode.Unauthorized);
        await AssertAudit(user.Id, "user.password_changed");
    }

    [Fact]
    public async Task Reset_ForcesChange_RevokesSession_EnforcesAdminAndAudits()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        var oldToken = client.DefaultRequestHeaders.Authorization!.Parameter!;
        using var admin = await factory.CreateAdminClientAsync();
        using var denied = await client.PostAsJsonAsync($"/api/v1/auth/users/{user.UserId}/password-reset", new { temporaryPassword = Initial }, JsonOptions, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var adminId = await factory.WithDbAsync(db => db.Users.Where(u => u.Rut == factory.AdminRut).Select(u => u.Id).SingleAsync(Ct));
        using var adminTarget = await admin.PostAsJsonAsync($"/api/v1/auth/users/{adminId}/password-reset", new { temporaryPassword = Initial }, JsonOptions, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, adminTarget.StatusCode);
        var reset = await Expect(await admin.PostAsJsonAsync($"/api/v1/auth/users/{user.UserId}/password-reset", new { temporaryPassword = Initial }, JsonOptions, Ct), HttpStatusCode.OK);
        Assert.True(reset.GetProperty("user").GetProperty("mustChangePassword").GetBoolean());
        await AssertRevoked(oldToken);
        using var fresh = factory.CreateClient();
        await Expect(await fresh.PostAsJsonAsync("/api/v1/auth/login", new { rut = user.Rut, password = Initial }, JsonOptions, Ct), HttpStatusCode.OK);
        await Expect(await fresh.GetAsync("/api/v1/schedules", Ct), HttpStatusCode.Forbidden, Pending);
        await AssertAudit(user.UserId, "user.password_reset");
    }

    [Fact]
    public async Task Logout_RevokesBearerAndCookieCopies()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        var token = client.DefaultRequestHeaders.Authorization!.Parameter!;
        await Expect(await client.PostAsync("/api/v1/auth/logout", null, Ct), HttpStatusCode.OK);
        await AssertRevoked(token);
    }

    [Fact]
    public async Task SessionVersion_IsRequired_AndPendingClaimComesFromDatabase()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        var id = user.UserId.ToString(CultureInfo.InvariantCulture);
        foreach (var version in new int?[] { null, 42 })
            await AssertRevoked(factory.CreateJwt(id, Roles.Usuario, user.Rut, sessionVersion: version));
        using var forgedPending = factory.CreateClientWithToken(factory.CreateJwt(id, Roles.Usuario, user.Rut, pendingClaim: true));
        await Expect(await forgedPending.GetAsync("/api/v1/schedules", Ct), HttpStatusCode.OK);
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Id == user.UserId, Ct)).MustChangePassword = true;
            await db.SaveChangesAsync(Ct);
        });
        await Expect(await client.GetAsync("/api/v1/schedules", Ct), HttpStatusCode.Forbidden, Pending);
    }

    private async Task AssertAudit(int id, string action)
    {
        var target = id.ToString(CultureInfo.InvariantCulture);
        var audit = await factory.WithDbAsync(db => db.AuditEvents.SingleAsync(a => a.TargetId == target && a.Action == action, Ct));
        Assert.Equal("[]", audit.Details);
    }
}
