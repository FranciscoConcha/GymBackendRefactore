using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using GymBackend.Application.Auth;
using GymBackend.Domain;
using GymBackend.IntegrationTests.Infrastructure;
using GymBackend.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Auth;

public sealed class AccountTests(GymApiFactory factory)
{
    private const string Password = "Nueva contraseña 123";

    private async Task<(int Id, string Rut, string Email, string Token)> PendingAsync()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var rut = TestData.NextRut();
        var email = $"{rut}@example.test";
        var json = await Expect(await admin.PostAsJsonAsync("/api/v1/auth/register", new
        {
            rut,
            firstName = "Cuenta",
            lastName = "Pendiente",
            email,
            role = Roles.Usuario,
            category = UserCategories.Estudiante,
        }, JsonOptions, Ct), HttpStatusCode.OK);
        var user = json.GetProperty("user");
        Assert.Equal("Pendiente", user.GetProperty("status").GetString());
        Assert.False(user.GetProperty("mustChangePassword").GetBoolean());
        var body = await factory.WithDbAsync(db => db.EmailOutbox.Where(e => e.To == email).Select(e => e.Body).SingleAsync(Ct));
        return (user.GetProperty("id").GetInt32(), rut, email, TokenFrom(body));
    }

    private static string TokenFrom(string body) => body.Split("token=", 2)[1].Split('\n')[0].Trim();

    [Fact]
    public async Task Activation_IsSingleUse_AndInvalidatesSiblingTokens()
    {
        var pending = await PendingAsync();
        using var client = factory.CreateClient();
        using var admin = await factory.CreateAdminClientAsync();
        await Expect(await admin.PostAsync($"/api/v1/auth/users/{pending.Id}/invitation", null, Ct), HttpStatusCode.OK);
        var bodies = await factory.WithDbAsync(db => db.EmailOutbox.Where(e => e.To == pending.Email).OrderBy(e => e.Id).Select(e => e.Body).ToArrayAsync(Ct));
        Assert.Equal(2, bodies.Length);
        var tokens = await factory.WithDbAsync(db => db.AccountTokens.Where(t => t.UserId == pending.Id).OrderBy(t => t.Id).ToArrayAsync(Ct));
        Assert.All(tokens, token => Assert.Equal(72, (token.ExpiresAt - token.CreatedAt).TotalHours));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pending.Token))), tokens[0].TokenHash);
        Assert.All(tokens, token => Assert.DoesNotContain(pending.Token, token.TokenHash));
        await Expect(await client.PostAsJsonAsync("/api/v1/auth/activate", new { token = pending.Token, newPassword = Password }, JsonOptions, Ct), HttpStatusCode.OK);
        foreach (var body in bodies)
            await Expect(await client.PostAsJsonAsync("/api/v1/auth/activate", new { token = TokenFrom(body), newPassword = Password }, JsonOptions, Ct),
                HttpStatusCode.BadRequest, AccountService.InvalidTokenMessage);
        var login = await Expect(await client.PostAsJsonAsync("/api/v1/auth/login", new { rut = pending.Rut, password = Password }, JsonOptions, Ct), HttpStatusCode.OK);
        Assert.Equal("Activa", login.GetProperty("user").GetProperty("status").GetString());
        Assert.False(login.GetProperty("user").GetProperty("mustChangePassword").GetBoolean());
        await Expect(await admin.PostAsync($"/api/v1/auth/users/{pending.Id}/invitation", null, Ct), HttpStatusCode.Conflict);
        var audits = await factory.WithDbAsync(db => db.AuditEvents.Where(a => a.TargetType == "User" && a.TargetId == pending.Id.ToString()).ToArrayAsync(Ct));
        Assert.Equal(2, audits.Count(a => a.Action == "user.invited"));
        Assert.Single(audits, a => a.Action == "user.activated");
        Assert.All(audits, a => { Assert.DoesNotContain(pending.Email, a.Details); Assert.DoesNotContain(pending.Token, a.Details); });
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("used")]
    [InlineData("tampered")]
    [InlineData("disabled")]
    public async Task Activation_RejectsUnusableToken(string condition)
    {
        var pending = await PendingAsync();
        await factory.WithDbAsync(async db =>
        {
            var token = await db.AccountTokens.SingleAsync(t => t.UserId == pending.Id, Ct);
            if (condition == "expired") token.ExpiresAt = token.CreatedAt;
            if (condition == "used") token.UsedAt = token.CreatedAt;
            if (condition == "disabled") (await db.Users.SingleAsync(u => u.Id == pending.Id, Ct)).Enabled = false;
            await db.SaveChangesAsync(Ct);
        });
        var value = condition == "tampered" ? (pending.Token[0] == 'a' ? "b" : "a") + pending.Token[1..] : pending.Token;
        using var client = factory.CreateClient();
        await Expect(await client.PostAsJsonAsync("/api/v1/auth/activate", new { token = value, newPassword = Password }, JsonOptions, Ct),
            HttpStatusCode.BadRequest, AccountService.InvalidTokenMessage);
        Assert.Equal(AccountStatus.Pendiente, await factory.WithDbAsync(db => db.Users.Where(u => u.Id == pending.Id).Select(u => u.AccountStatus).SingleAsync(Ct)));
    }

    [Fact]
    public async Task PendingAccount_CannotLogin_EvenWithMatchingHashOrForgedSession()
    {
        var pending = await PendingAsync();
        await factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == pending.Id, Ct);
            user.PasswordHash = new Pbkdf2PasswordHasher().Hash(Password);
            await db.SaveChangesAsync(Ct);
        });
        using var client = factory.CreateClient();
        await Expect(await client.PostAsJsonAsync("/api/v1/auth/login", new { rut = pending.Rut, password = Password }, JsonOptions, Ct),
            HttpStatusCode.Unauthorized, "Credenciales de usuario incorrectas.");
        using var forged = factory.CreateClientWithToken(factory.CreateJwt(pending.Id, Roles.Usuario, pending.Rut));
        using var denied = await forged.GetAsync("/api/v1/auth/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    [Fact]
    public async Task Reset_AlwaysReturnsIdenticalAcceptedResponse_OnlyEnabledActiveAccountsGetMail()
    {
        var pending = await PendingAsync();
        var user = await factory.CreateClientUserAsync();
        using var existing = user.Client;
        using var admin = await factory.CreateAdminClientAsync();
        await Expect(await Status(admin, user.UserId, false), HttpStatusCode.OK);
        var disabledEmail = $"user-{user.Rut}@example.test";
        using var client = factory.CreateClient();
        foreach (var email in new[] { $"missing-{Guid.NewGuid():N}@example.test", pending.Email, disabledEmail, "invalid", "" })
            await Expect(await client.PostAsJsonAsync("/api/v1/auth/password-resets", new { email }, JsonOptions, Ct),
                HttpStatusCode.Accepted, AccountService.ResetRequestedMessage);
        Assert.Equal(0, await factory.WithDbAsync(db => db.AccountTokens.CountAsync(t =>
            (t.UserId == pending.Id || t.UserId == user.UserId) && t.Purpose == AccountTokenPurpose.PasswordReset, Ct)));
    }

    [Fact]
    public async Task Reset_LimitCountsRecentTokensIncludingUsed_AndExpiresAfterOneHour()
    {
        var user = await factory.CreateClientUserAsync();
        using var existing = user.Client;
        using var client = factory.CreateClient();
        var email = $"user-{user.Rut}@example.test";
        // Concurrent requests must not bypass the per-account limit.
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            client.PostAsJsonAsync("/api/v1/auth/password-resets", new { email }, JsonOptions, Ct)));
        foreach (var response in responses) await Expect(response, HttpStatusCode.Accepted, AccountService.ResetRequestedMessage);
        var tokens = await factory.WithDbAsync(db => db.AccountTokens.Where(t => t.UserId == user.UserId).ToArrayAsync(Ct));
        Assert.Equal(3, tokens.Length);
        Assert.All(tokens, t => Assert.Equal(60, (t.ExpiresAt - t.CreatedAt).TotalMinutes));
        await factory.WithDbAsync(async db =>
        {
            var recent = await db.AccountTokens.Where(t => t.UserId == user.UserId).ToArrayAsync(Ct);
            foreach (var token in recent) token.UsedAt = token.CreatedAt;
            await db.SaveChangesAsync(Ct);
        });
        await Expect(await client.PostAsJsonAsync("/api/v1/auth/password-resets", new { email }, JsonOptions, Ct), HttpStatusCode.Accepted);
        Assert.Equal(3, await factory.WithDbAsync(db => db.EmailOutbox.CountAsync(e => e.To == email, Ct)));
        await factory.WithDbAsync(async db =>
        {
            var recent = await db.AccountTokens.Where(t => t.UserId == user.UserId).ToArrayAsync(Ct);
            foreach (var token in recent) token.CreatedAt = token.CreatedAt.AddHours(-2);
            await db.SaveChangesAsync(Ct);
        });
        await Expect(await client.PostAsJsonAsync("/api/v1/auth/password-resets", new { email }, JsonOptions, Ct), HttpStatusCode.Accepted);
        Assert.Equal(4, await factory.WithDbAsync(db => db.EmailOutbox.CountAsync(e => e.To == email, Ct)));
    }

    [Fact]
    public async Task ResetConfirmation_RevokesSessions_ClearsMandatoryChange_AndConsumesAllResetTokens()
    {
        var user = await factory.CreateClientUserAsync();
        using var oldSession = user.Client;
        using var client = factory.CreateClient();
        var email = $"user-{user.Rut}@example.test";
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Id == user.UserId, Ct)).MustChangePassword = true;
            await db.SaveChangesAsync(Ct);
        });
        for (var i = 0; i < 2; i++)
            await Expect(await client.PostAsJsonAsync("/api/v1/auth/password-resets", new { email }, JsonOptions, Ct), HttpStatusCode.Accepted);
        var bodies = await factory.WithDbAsync(db => db.EmailOutbox.Where(e => e.To == email).OrderBy(e => e.Id).Select(e => e.Body).ToArrayAsync(Ct));
        var token = TokenFrom(bodies[0]);
        await Expect(await client.PostAsJsonAsync("/api/v1/auth/activate", new { token, newPassword = Password }, JsonOptions, Ct),
            HttpStatusCode.BadRequest, AccountService.InvalidTokenMessage);
        await Expect(await client.PostAsJsonAsync("/api/v1/auth/password-resets/confirm", new { token, newPassword = Password }, JsonOptions, Ct), HttpStatusCode.OK);
        using var revoked = await oldSession.GetAsync("/api/v1/auth/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        foreach (var body in bodies)
            await Expect(await client.PostAsJsonAsync("/api/v1/auth/password-resets/confirm", new { token = TokenFrom(body), newPassword = Password }, JsonOptions, Ct),
                HttpStatusCode.BadRequest, AccountService.InvalidTokenMessage);
        await Expect(await client.PostAsJsonAsync("/api/v1/auth/login", new { rut = user.Rut, password = user.Password }, JsonOptions, Ct), HttpStatusCode.Unauthorized);
        var login = await Expect(await client.PostAsJsonAsync("/api/v1/auth/login", new { rut = user.Rut, password = Password }, JsonOptions, Ct), HttpStatusCode.OK);
        Assert.False(login.GetProperty("user").GetProperty("mustChangePassword").GetBoolean());
        var audits = await factory.WithDbAsync(db => db.AuditEvents.Where(a => a.TargetType == "User" && a.TargetId == user.UserId.ToString()).ToArrayAsync(Ct));
        Assert.Equal(2, audits.Count(a => a.Action == "user.password_reset_requested"));
        Assert.All(audits, a => { Assert.DoesNotContain(email, a.Details); Assert.DoesNotContain(token, a.Details); });
    }

    [Theory]
    [InlineData(7)]
    [InlineData(129)]
    public async Task Activation_ValidatesPasswordLength_WithoutConsumingToken(int length)
    {
        var pending = await PendingAsync();
        using var client = factory.CreateClient();
        await Expect(await client.PostAsJsonAsync("/api/v1/auth/activate", new { token = pending.Token, newPassword = new string('a', length) }, JsonOptions, Ct), HttpStatusCode.BadRequest);
        await Expect(await client.PostAsJsonAsync("/api/v1/auth/activate", new { token = pending.Token, newPassword = Password }, JsonOptions, Ct), HttpStatusCode.OK);
    }

    [Fact]
    public async Task Invitation_UsesConfiguredFrontendUrl_NeverRequestHost()
    {
        using var host = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Accounts:FrontendBaseUrl"] = "https://frontend.example/base" })));
        var dedicated = await factory.CreateDedicatedAdminClientAsync(host);
        using var admin = dedicated.Client;
        admin.DefaultRequestHeaders.Host = "attacker.example";
        var rut = TestData.NextRut();
        await Expect(await admin.PostAsJsonAsync("/api/v1/auth/register", new
        {
            rut,
            firstName = "Invitado",
            lastName = "Prueba",
            email = $"{rut}@example.test",
            role = Roles.Usuario,
            category = UserCategories.Externo,
        }, JsonOptions, Ct), HttpStatusCode.OK);
        var mail = await factory.WithDbAsync(db => db.EmailOutbox.SingleAsync(e => e.To == $"{rut}@example.test", Ct));
        Assert.Contains("https://frontend.example/base/activar?token=", mail.Body);
        Assert.DoesNotContain("attacker.example", mail.Body);
        Assert.Null(mail.SentAt);
        Assert.Equal(0, mail.AttemptCount);
    }
}
