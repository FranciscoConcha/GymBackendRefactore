using System.Net;
using System.Net.Http.Json;
using GymBackend.Application.Auth;
using GymBackend.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Auth;

public sealed class AuthTests(GymApiFactory factory)
{
    private static object Registration(string rut, string name = "Test", string? email = null, string password = "Password123!", string role = "Usuario") =>
        new { rut, firstName = name, lastName = "Test", email = email ?? $"{rut}@example.test", password, role, category = role == "Usuario" ? "Estudiante" : null };

    [Theory]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.Forbidden)]
    public async Task Register_SinPermiso_Rechazado(bool authenticated, HttpStatusCode expected)
    {
        var user = authenticated ? await factory.CreateClientUserAsync() : default;
        using var client = authenticated ? user.Client : factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", Registration(TestData.NextRut()), JsonOptions, Ct);
        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("1234567")]
    [InlineData("12.345.678-9")]
    public async Task Register_RutInvalido_BadRequest(string rut)
    {
        using var admin = await factory.CreateAdminClientAsync();
        await Expect(await admin.PostAsJsonAsync("/api/v1/auth/register", Registration(rut), JsonOptions, Ct), HttpStatusCode.BadRequest,
            RutFormat.ValidationMessage);
    }

    [Theory]
    [InlineData("", "valid@example.test", "password")]
    [InlineData("Name", "", "password")]
    [InlineData("Name", "valid@example.test", "")]
    public async Task Register_CampoVacio_BadRequest(string name, string email, string password)
    {
        using var admin = await factory.CreateAdminClientAsync();
        var rut = TestData.NextRut();
        await Expect(await admin.PostAsJsonAsync("/api/v1/auth/register", Registration(rut, name, email == "" ? "" : $"{rut}@example.test", password), JsonOptions, Ct),
            HttpStatusCode.BadRequest, "Todos los campos son obligatorios.");
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("ADMIN")]
    public async Task Register_TipoAdministrador_BadRequest(string type)
    {
        using var admin = await factory.CreateAdminClientAsync();
        await Expect(await admin.PostAsJsonAsync("/api/v1/auth/register", Registration(TestData.NextRut(), role: type), JsonOptions, Ct),
            HttpStatusCode.BadRequest, "Los administradores se crean por invitación, sin contraseña temporal.");
    }

    [Fact]
    public async Task Register_Duplicados_BadRequest()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        using var admin = await factory.CreateAdminClientAsync();
        await Expect(await admin.PostAsJsonAsync("/api/v1/auth/register", Registration(user.Rut), JsonOptions, Ct), HttpStatusCode.BadRequest, "El RUT ya está registrado en el sistema.");
        await Expect(await admin.PostAsJsonAsync("/api/v1/auth/register", Registration(TestData.NextRut(), email: $"USER-{user.Rut}@EXAMPLE.TEST"), JsonOptions, Ct), HttpStatusCode.BadRequest, "El correo ya está registrado en el sistema.");
    }

    [Fact]
    public async Task Register_Valido_NormalizaUsuarioYGuardaPbkdf2()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var rut = TestData.NextRut();
        const string password = "Password123!";
        var result = await Expect(await admin.PostAsJsonAsync("/api/v1/auth/register", Registration(rut, email: $"  USER-{rut}@EXAMPLE.TEST  ", password: password), JsonOptions, Ct), HttpStatusCode.OK, "Usuario registrado correctamente.");
        var user = result.GetProperty("user");
        Assert.Equal("Usuario", user.GetProperty("role").GetString());
        Assert.Equal("", user.GetProperty("username").GetString());
        Assert.True(user.GetProperty("enabled").GetBoolean());
        // El RUT puede terminar en K: el correo completo se normaliza a minúsculas.
        Assert.Equal($"user-{rut.ToLowerInvariant()}@example.test", user.GetProperty("email").GetString());
        var hash = await factory.WithDbAsync(db => db.Users.Where(u => u.Rut == rut).Select(u => u.PasswordHash).SingleAsync(Ct));
        Assert.NotEqual(32, Convert.FromBase64String(hash).Length);
        Assert.Equal(PasswordCheck.Success, new Pbkdf2PasswordHasher().Verify(password, hash));
    }

    [Theory]
    [InlineData("1234567", "password", HttpStatusCode.BadRequest, RutFormat.ValidationMessage)]
    [InlineData("12.345.678-9", "password", HttpStatusCode.BadRequest, RutFormat.ValidationMessage)]
    [InlineData("12345678-5", "", HttpStatusCode.BadRequest, "La contraseña es obligatoria.")]
    public async Task Login_DatosInvalidos_Rechazado(string rut, string password, HttpStatusCode status, string message)
    {
        using var client = factory.CreateClient();
        await Expect(await client.PostAsJsonAsync("/api/v1/auth/login", new { rut, password }, JsonOptions, Ct), status, message);
    }

    [Fact]
    public async Task Login_Credenciales_VerificaExitoIncorrectaYAdmin()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        var json = await Expect(await client.PostAsJsonAsync("/api/v1/auth/login", new { rut = user.Rut, password = user.Password }, JsonOptions, Ct), HttpStatusCode.OK, "Login correcto.");
        Assert.False(json.TryGetProperty("token", out _));
        Assert.True(json.GetProperty("expiresAt").GetDateTimeOffset() > DateTimeOffset.UtcNow);
        Assert.Equal(user.UserId, json.GetProperty("user").GetProperty("id").GetInt32());
        await Expect(await client.PostAsJsonAsync("/api/v1/auth/login", new { rut = user.Rut, password = "incorrecta" }, JsonOptions, Ct), HttpStatusCode.Unauthorized, "Credenciales de usuario incorrectas.");
        await Expect(await client.PostAsJsonAsync("/api/v1/auth/login", new { rut = factory.AdminRut, password = factory.AdminPassword }, JsonOptions, Ct), HttpStatusCode.Unauthorized, "Credenciales de usuario incorrectas.");
    }

    [Fact]
    public async Task Login_Deshabilitado_Unauthorized()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        using var admin = await factory.CreateAdminClientAsync();
        await Expect(await Status(admin, user.UserId, false), HttpStatusCode.OK);
        await Expect(await client.PostAsJsonAsync("/api/v1/auth/login", new { rut = user.Rut, password = user.Password }, JsonOptions, Ct), HttpStatusCode.Unauthorized, "Credenciales de usuario incorrectas.");
    }

    [Fact]
    public async Task RegisterAndLogin_DottedRut_NormalizesToCanonical()
    {
        const string rut = "12.345.678-5";
        const string canonical = "12345678-5";
        const string password = "Password123!";
        using var admin = await factory.CreateAdminClientAsync();
        var registered = await Expect(await admin.PostAsJsonAsync("/api/v1/auth/register",
            Registration(rut, email: $"normalized-{Guid.NewGuid():N}@example.test", password: password), JsonOptions, Ct), HttpStatusCode.OK);
        Assert.Equal(canonical, registered.GetProperty("user").GetProperty("rut").GetString());
        Assert.True(await factory.WithDbAsync(db => db.Users.AnyAsync(u => u.Rut == canonical, Ct)));
        using var client = factory.CreateClient();
        await Expect(await client.PostAsJsonAsync("/api/v1/auth/login", new { rut, password }, JsonOptions, Ct), HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("", "password", HttpStatusCode.BadRequest, "El usuario y la contraseña son obligatorios.")]
    [InlineData("user", "", HttpStatusCode.BadRequest, "El usuario y la contraseña son obligatorios.")]
    [InlineData("incorrecto", "incorrecta", HttpStatusCode.Unauthorized, "Credenciales de administrador incorrectas.")]
    public async Task AdminLogin_DatosInvalidos_Rechazado(string username, string password, HttpStatusCode status, string message)
    {
        using var client = factory.CreateClient();
        await Expect(await client.PostAsJsonAsync("/api/v1/auth/admin-login", new { username, password }, JsonOptions, Ct), status, message);
    }

    [Fact]
    public async Task AdminLogin_Mayusculas_Ok()
    {
        using var client = factory.CreateClient();
        var json = await Expect(await client.PostAsJsonAsync("/api/v1/auth/admin-login", new { username = factory.AdminUsername.ToUpperInvariant(), password = factory.AdminPassword }, JsonOptions, Ct), HttpStatusCode.OK, "Login de administrador correcto.");
        Assert.Equal("Admin", json.GetProperty("user").GetProperty("role").GetString());
        Assert.False(json.TryGetProperty("token", out _));
        Assert.True(json.GetProperty("expiresAt").GetDateTimeOffset() > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Users_PermisosOrdenYPrivacidad_ComportamientoActual()
    {
        using var anonymous = factory.CreateClient();
        using var denied = await anonymous.GetAsync("/api/v1/auth/users", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        using var forbidden = await client.GetAsync("/api/v1/auth/users", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var admin = await factory.CreateAdminClientAsync();
        var users = (await Expect(await admin.GetAsync("/api/v1/auth/users", Ct), HttpStatusCode.OK)).EnumerateArray().ToArray();
        Assert.Contains(users, u => u.GetProperty("id").GetInt32() == user.UserId);
        Assert.All(users, u => Assert.False(u.TryGetProperty("passwordHash", out _)));
        // Usa la colación real de PostgreSQL y solo los IDs de esta respuesta:
        // nuevas altas paralelas no alteran la comparación.
        var ids = users.Select(u => u.GetProperty("id").GetInt32()).ToArray();
        var orderedIds = await factory.WithDbAsync(db => db.Users.Where(u => ids.Contains(u.Id)).OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ThenBy(u => u.Id).Select(u => u.Id).ToArrayAsync(Ct));
        Assert.Equal(orderedIds, ids);
    }

    [Fact]
    public async Task Status_PermisosYTransiciones_ComportamientoActual()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        using var anonymous = factory.CreateClient();
        using var unauthorized = await Status(anonymous, user.UserId, false);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        using var forbidden = await Status(client, user.UserId, false);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var admin = await factory.CreateAdminClientAsync();
        await Expect(await Status(admin, int.MaxValue, false), HttpStatusCode.NotFound, "Usuario no encontrado.");
        var adminId = await factory.WithDbAsync(db => db.Users.Where(u => u.Rut == factory.AdminRut).Select(u => u.Id).SingleAsync(Ct));
        await Expect(await Status(admin, adminId, false), HttpStatusCode.BadRequest, "No se puede deshabilitar a un administrador.");
        foreach (var enabled in new[] { false, true })
        {
            var json = await Expect(await Status(admin, user.UserId, enabled), HttpStatusCode.OK, enabled ? "Usuario habilitado correctamente." : "Usuario deshabilitado correctamente.");
            Assert.Equal(enabled, json.GetProperty("user").GetProperty("enabled").GetBoolean());
        }
    }
}
