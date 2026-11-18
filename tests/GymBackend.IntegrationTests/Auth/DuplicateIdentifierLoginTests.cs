using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using GymBackend.Domain;
using GymBackend.IntegrationTests.Infrastructure;
using GymBackend.Models;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Auth;

public sealed class DuplicateIdentifierLoginTests(GymApiFactory factory)
{
    [Fact]
    public async Task Login_RutUnico_AutenticaLaCuentaConLaContrasenaCoincidente()
    {
        var rut = TestData.NextRut();
        var secondRut = TestData.NextRut();
        const string firstPassword = "FirstPassword123!";
        const string secondPassword = "SecondPassword123!";
        var ids = await factory.WithDbAsync(async db =>
        {
            var first = NewUser(rut, $"first-{rut}@example.test", firstPassword);
            var second = NewUser(rut, $"second-{rut}@example.test", secondPassword);
            db.Users.Add(first);
            await db.SaveChangesAsync(Ct);
            db.Users.Add(second);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
            second.Rut = secondRut;
            await db.SaveChangesAsync(Ct);
            return new[] { first.Id, second.Id };
        });

        using var client = factory.CreateClient();
        var result = await Expect(await client.PostAsJsonAsync("/api/v1/auth/login",
            new { rut = secondRut, password = secondPassword }, JsonOptions, Ct), HttpStatusCode.OK, "Login correcto.");
        Assert.Equal(ids[1], result.GetProperty("user").GetProperty("id").GetInt32());
        await Expect(await client.PostAsJsonAsync("/api/v1/auth/login",
            new { rut = secondRut, password = "IncorrectPassword!" }, JsonOptions, Ct), HttpStatusCode.Unauthorized,
            "Credenciales de usuario incorrectas.");
    }

    [Fact]
    public async Task AdminLogin_UsernameUnico_AutenticaElAdminConLaContrasenaCoincidente()
    {
        var username = $"duplicate-{Guid.NewGuid():N}";
        var secondUsername = $"unique-{Guid.NewGuid():N}";
        const string firstPassword = "FirstAdminPassword123!";
        const string secondPassword = "SecondAdminPassword123!";
        var ids = await factory.WithDbAsync(async db =>
        {
            var first = NewUser(TestData.NextRut(), $"first-{username}@example.test", firstPassword,
                username, Roles.Admin);
            var second = NewUser(TestData.NextRut(), $"second-{username}@example.test", secondPassword,
                username, Roles.Admin);
            db.Users.Add(first);
            await db.SaveChangesAsync(Ct);
            db.Users.Add(second);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
            second.Username = secondUsername;
            await db.SaveChangesAsync(Ct);
            return new[] { first.Id, second.Id };
        });

        using var client = factory.CreateClient();
        var result = await Expect(await client.PostAsJsonAsync("/api/v1/auth/admin-login",
            new { username = secondUsername, password = secondPassword }, JsonOptions, Ct), HttpStatusCode.OK,
            "Login de administrador correcto.");
        Assert.Equal(ids[1], result.GetProperty("user").GetProperty("id").GetInt32());
    }

    private static User NewUser(string rut, string email, string password, string username = "", string role = Roles.Client) => new()
    {
        Rut = rut,
        Name = $"Duplicate {rut}",
        Email = email,
        Username = username,
        PasswordHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password))),
        Role = role,
        UserType = role == Roles.Admin ? UserTypes.Administrator : UserTypes.Student,
        Enabled = true,
    };
}
