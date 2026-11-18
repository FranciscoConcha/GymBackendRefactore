using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using GymBackend.IntegrationTests.Infrastructure;
using GymBackend.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Security;

public sealed class SessionCookieTests(GymApiFactory factory)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Login_IssuesPrivateCookieWithMatchingExpiry(bool admin)
    {
        var user = await factory.CreateClientUserAsync();
        using var existing = user.Client;
        using var client = factory.CreateClient();
        var before = DateTimeOffset.UtcNow;
        using var response = admin
            ? await client.PostAsJsonAsync("/api/v1/auth/admin-login", new { username = factory.AdminUsername, password = factory.AdminPassword }, JsonOptions, Ct)
            : await client.PostAsJsonAsync("/api/v1/auth/login", new { rut = user.Rut, password = user.Password }, JsonOptions, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        var parts = cookie.Split(';', StringSplitOptions.TrimEntries);
        Assert.StartsWith(SessionCookie.Name + "=", parts[0]);
        Assert.Contains(parts, part => part.Equals("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(parts, part => part.Equals("secure", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(parts, part => part.Equals("samesite=strict", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(parts, part => part.Equals("path=/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(parts, part => part.StartsWith("domain=", StringComparison.OrdinalIgnoreCase));
        var token = parts[0].Split('=', 2)[1];
        var expires = DateTimeOffset.Parse(parts.Single(part => part.StartsWith("expires=", StringComparison.OrdinalIgnoreCase))[8..], CultureInfo.InvariantCulture);
        Assert.InRange(expires, before.AddHours(8).AddSeconds(-1), DateTimeOffset.UtcNow.AddHours(8));
        Assert.Equal(expires.UtcDateTime, new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain(token, body);
        var json = await Read(response);
        Assert.False(json.TryGetProperty("token", out _));
        Assert.Equal(expires, json.GetProperty("expiresAt").GetDateTimeOffset());
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var me = await client.GetAsync("/api/v1/auth/me", Ct);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(admin ? factory.AdminRut : user.Rut, (await Read(me)).GetProperty("rut").GetString());
        using var logout = await client.PostAsync("/api/v1/auth/logout", null, Ct);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Equal("Sesión cerrada.", (await Read(logout)).GetProperty("message").GetString());
        var deleted = Assert.Single(logout.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith(SessionCookie.Name + "=;", deleted);
        Assert.Contains("expires=Thu, 01 Jan 1970", deleted);
        using var denied = await client.GetAsync("/api/v1/auth/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    [Fact]
    public async Task InvalidMissingAndQueryTokens_CannotAuthenticate()
    {
        var user = await factory.CreateClientUserAsync();
        using var existing = user.Client;
        var token = factory.CreateJwt(user.UserId, "Client", user.Rut);
        var parts = token.Split('.');
        parts[1] = (parts[1][0] == 'a' ? "b" : "a") + parts[1][1..];
        foreach (var invalid in new[] { "", string.Join('.', parts), factory.CreateJwt(user.UserId.ToString(CultureInfo.InvariantCulture), "Client", user.Rut, signingKey: new string('x', 64)) })
        {
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
            if (invalid.Length > 0) client.DefaultRequestHeaders.Add("Cookie", SessionCookie.Name + "=" + invalid);
            using var response = await client.GetAsync("/api/v1/auth/me", Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        using var anonymous = factory.CreateClient();
        foreach (var name in new[] { "access_token", "token" })
        {
            using var response = await anonymous.GetAsync($"/api/v1/auth/me?{name}={token}", Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task DisabledUser_WithPreviousCookie_IsRejected()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        client.DefaultRequestHeaders.Authorization = null;
        using (var before = await client.GetAsync("/api/v1/auth/me", Ct))
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        using var admin = await factory.CreateAdminClientAsync();
        await Expect(await Status(admin, user.UserId, false), HttpStatusCode.OK);
        using var response = await client.GetAsync("/api/v1/auth/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
