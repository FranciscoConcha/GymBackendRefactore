using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using GymBackend.Application.Auth;
using GymBackend.Domain;
using GymBackend.IntegrationTests.Infrastructure;
using GymBackend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Auth;

public sealed class PasswordMigrationTests(GymApiFactory factory)
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Login_RehashesOnlyOnSuccess(bool admin, bool identityV2)
    {
        var rut = TestData.NextRut();
        var username = $"migration-{Guid.NewGuid():N}";
        const string password = "MigrationPassword123!";
        var oldHash = identityV2
            ? new PasswordHasher<User>(Microsoft.Extensions.Options.Options.Create(new PasswordHasherOptions
                { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2 })).HashPassword(new User(), password)
            : Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
        await factory.WithDbAsync(async db =>
        {
            db.Users.Add(new User
            {
                Rut = rut, Name = "Migration", Email = $"{username}@example.test",
                Username = admin ? username : "", PasswordHash = oldHash, Enabled = true,
                Role = admin ? Roles.Admin : Roles.Client,
                UserType = admin ? UserTypes.Administrator : UserTypes.Student,
            });
            await db.SaveChangesAsync(Ct);
            return true;
        });
        using var client = factory.CreateClient();
        var endpoint = admin ? "/api/v1/auth/admin-login" : "/api/v1/auth/login";
        object Credentials(string secret) => admin ? new { username, password = secret } : (object)new { rut, password = secret };
        async Task<string> StoredHash() => await factory.WithDbAsync(db => db.Users.Where(u => u.Rut == rut).Select(u => u.PasswordHash).SingleAsync(Ct));

        await Expect(await client.PostAsJsonAsync(endpoint, Credentials("wrong"), JsonOptions, Ct), HttpStatusCode.Unauthorized);
        Assert.Equal(oldHash, await StoredHash());
        await factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Rut == rut, Ct);
            user.Enabled = false;
            await db.SaveChangesAsync(Ct);
            return true;
        });
        await Expect(await client.PostAsJsonAsync(endpoint, Credentials(password), JsonOptions, Ct), HttpStatusCode.Unauthorized,
            admin ? "Credenciales de administrador incorrectas." : "Credenciales de usuario incorrectas.");
        Assert.Equal(oldHash, await StoredHash());
        await factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Rut == rut, Ct);
            user.Enabled = true;
            await db.SaveChangesAsync(Ct);
            return true;
        });
        await Expect(await client.PostAsJsonAsync(endpoint, Credentials(password), JsonOptions, Ct), HttpStatusCode.OK);
        var migrated = await StoredHash();
        Assert.NotEqual(oldHash, migrated);
        Assert.NotEqual(32, Convert.FromBase64String(migrated).Length);
        Assert.Equal(PasswordCheck.Success, new Pbkdf2PasswordHasher().Verify(password, migrated));
        await Expect(await client.PostAsJsonAsync(endpoint, Credentials(password), JsonOptions, Ct), HttpStatusCode.OK);
        Assert.Equal(migrated, await StoredHash());
    }
}
