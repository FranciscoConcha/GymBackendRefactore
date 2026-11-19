using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GymBackend.Data;
using GymBackend.Domain;
using GymBackend.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(GymBackend.IntegrationTests.Infrastructure.GymApiFactory))]

namespace GymBackend.IntegrationTests.Infrastructure;

public sealed class GymApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
#pragma warning disable CS0618 // Compatible con el constructor de Testcontainers 4.x.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();
#pragma warning restore CS0618
    private readonly Dictionary<string, string?> _previousEnvironment = new();

    public WebApplicationFactory<Program> WithClock(FakeTimeProvider clock) =>
        WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(clock)));

    public WebApplicationFactory<Program> WithClockAndSettings(FakeTimeProvider clock, IDictionary<string, string?> settings) =>
        WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
            b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(clock));
        });

    public string JwtKey { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public string QrKey { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public string AdminRut { get; } = TestData.NextRut();
    public string AdminEmail { get; } = $"admin-{Guid.NewGuid():N}@example.test";
    public string AdminUsername { get; } = $"admin-{Guid.NewGuid():N}";
    public string AdminPassword { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

    public async ValueTask InitializeAsync()
    {
        try
        {
            await _postgres.StartAsync(TestContext.Current.CancellationToken);
            SetEnvironment("ConnectionStrings__DefaultConnection", _postgres.GetConnectionString());
            SetEnvironment("Jwt__Key", JwtKey);
            SetEnvironment("Jwt__Issuer", "gym-tests");
            SetEnvironment("Jwt__Audience", "gym-tests-clients");
            SetEnvironment("Qr__SigningKey", QrKey);
            // Umbral alto: las pruebas inician sesiÃ³n muchas veces; las de lÃ­mite usan un host derivado.
            SetEnvironment("RateLimiting__Login__PermitLimit", "100000");
            SetEnvironment("Database__Initialize", "true");
            SetEnvironment("Bootstrap__AdminRut", AdminRut);
            SetEnvironment("Bootstrap__AdminEmail", AdminEmail);
            SetEnvironment("Bootstrap__AdminUsername", AdminUsername);
            SetEnvironment("Bootstrap__AdminPassword", AdminPassword);
            _ = Server;
            // Migraciones y semilla reales (Database:Initialize); se añade una rama fija para las pruebas.
            await WithDbAsync(async db =>
            {
                if (!await db.SportBranches.AnyAsync(b => b.Name == "Fixture", TestContext.Current.CancellationToken))
                {
                    db.SportBranches.Add(new SportBranch { Name = "Fixture", CreatedAt = DateTimeOffset.UtcNow });
                    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
                }
            });
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    // CreateClient reasigna BaseAddress desde ClientOptions despuÃ©s de ConfigureClient: se fija aquÃ­ para que
    // la cookie Secure de sesiÃ³n viaje por https (los hosts derivados copian ClientOptions).
    public GymApiFactory() => ClientOptions.BaseAddress = new Uri("https://localhost/");

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Add("X-Gym-Csrf", "1");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseEnvironment("Development");

    public override async ValueTask DisposeAsync()
    {
        try
        {
            await _postgres.DisposeAsync();
        }
        finally
        {
            try
            {
                await base.DisposeAsync();
            }
            finally
            {
                foreach (var (name, value) in _previousEnvironment)
                    Environment.SetEnvironmentVariable(name, value);
                _previousEnvironment.Clear();
            }
        }
    }

    public async Task<(HttpClient Client, int UserId, string Rut, string Username, string Password)> CreateDedicatedAdminClientAsync(
        WebApplicationFactory<Program>? host = null)
    {
        var rut = TestData.NextRut();
        var username = $"admin-{Guid.NewGuid():N}";
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var userId = await WithDbAsync(async db =>
        {
            var user = new User
            {
                Rut = rut,
                Username = username,
                Email = $"{rut}@example.test",
                FirstName = "Dedicated",
                LastName = "Admin",
                Role = Roles.Admin,
                PasswordHash = new GymBackend.Application.Auth.Pbkdf2PasswordHasher().Hash(password),
                CreatedAt = Services.GetRequiredService<GymBackend.Time.IGymClock>().UtcNow,
                UpdatedAt = Services.GetRequiredService<GymBackend.Time.IGymClock>().UtcNow,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return user.Id;
        });
        // La sesión se emite con el reloj real: un host con reloj simulado emitiría un JWT ya vencido para el validador.
        using var login = CreateClient();
        var client = (host ?? this).CreateClient();
        client.BaseAddress = new Uri("https://localhost/");
        client.DefaultRequestHeaders.Remove("X-Gym-Csrf");
        client.DefaultRequestHeaders.Add("X-Gym-Csrf", "1");
        try
        {
            using var response = await login.PostAsJsonAsync("/api/v1/auth/admin-login", new { username, password },
                JsonOptions, TestContext.Current.CancellationToken);
            _ = await ReadAuthAsync(response);
            SetToken(client, ReadSessionToken(response));
            return (client, userId, rut, username, password);
        }
        catch { client.Dispose(); throw; }
    }

    public async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = CreateClient();
        try
        {
            using var response = await client.PostAsJsonAsync("/api/v1/auth/admin-login",
                new { username = AdminUsername, password = AdminPassword }, JsonOptions, TestContext.Current.CancellationToken);
            _ = await ReadAuthAsync(response);
            SetToken(client, ReadSessionToken(response));
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public async Task<(HttpClient Client, int UserId, string Rut, string Password)> CreateClientUserAsync(
        string category = "Estudiante")
    {
        var rut = TestData.NextRut();
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        using var admin = await CreateAdminClientAsync();
        using var registration = await admin.PostAsJsonAsync("/api/v1/auth/register", new
        {
            rut,
            firstName = "Test",
            lastName = rut,
            email = $"user-{rut}@example.test",
            password,
            role = "Usuario",
            category,
        }, JsonOptions, TestContext.Current.CancellationToken);
        var registered = await ReadAuthAsync(registration);
        var userId = registered.User?.Id
            ?? throw new InvalidOperationException("El registro no devolviÃ³ el usuario.");
        await MakeFixtureUserReadyAsync(registered.User!.Id);
        var client = CreateClient();
        try
        {
            using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { rut, password }, JsonOptions, TestContext.Current.CancellationToken);
            _ = await ReadAuthAsync(login);
            SetToken(client, ReadSessionToken(login));
            return (client, userId, rut, password);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public async Task<(HttpClient Client, int UserId, string Rut, string Username, string Password)> CreateReceptionistClientAsync()
    {
        var rut = TestData.NextRut();
        var username = $"reception-{Guid.NewGuid():N}";
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        using var admin = await CreateAdminClientAsync();
        using var registration = await admin.PostAsJsonAsync("/api/v1/auth/register", new
        {
            rut,
            firstName = "Reception",
            lastName = "Test",
            email = $"{rut}@example.test",
            password,
            username = $" {username.ToUpperInvariant()} ",
            role = Roles.Recepcionista,
        }, JsonOptions, TestContext.Current.CancellationToken);
        var registered = await ReadAuthAsync(registration);
        Assert.Equal(username, registered.User!.Username);
        await MakeFixtureUserReadyAsync(registered.User!.Id);
        var client = CreateClient();
        try
        {
            using var login = await client.PostAsJsonAsync("/api/v1/auth/admin-login", new { username, password }, JsonOptions, TestContext.Current.CancellationToken);
            var authenticated = await ReadAuthAsync(login);
            Assert.Equal(Roles.Recepcionista, authenticated.User!.Role);
            SetToken(client, ReadSessionToken(login));
            return (client, registered.User!.Id, rut, username, password);
        }
        catch { client.Dispose(); throw; }
    }

    // Ordinary fixture users bypass only the initial password step in the database.
    // Security tests register directly, preserving coverage of the pending-password flow.
    private Task MakeFixtureUserReadyAsync(int id) => WithDbAsync(async db =>
    {
        var user = await db.Users.FindAsync([id], TestContext.Current.CancellationToken);
        user!.MustChangePassword = false;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    });

    public HttpClient CreateClientWithToken(string token)
    {
        var client = CreateClient();
        SetToken(client, token);
        return client;
    }

    public string CreateJwt(int userId, string role, string rut, string name = "Test", DateTime? expires = null)
        => CreateJwt(userId.ToString(CultureInfo.InvariantCulture), role, rut, name, expires);

    public string CreateJwt(string? userId, string role, string rut, string name = "Test", DateTime? expires = null,
        string? issuer = "gym-tests", string? audience = "gym-tests-clients", string? signingKey = null, int? sessionVersion = 0, bool pendingClaim = false)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, name),
            new Claim(ClaimTypes.Role, role),
            new Claim("rut", rut),
            new Claim("email", $"user-{rut}@example.test"),
        };
        if (sessionVersion is { } version) claims.Add(new Claim("sv", version.ToString(CultureInfo.InvariantCulture)));
        if (pendingClaim) claims.Add(new Claim("gym:pending-password-change", "true"));
        if (userId is not null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey ?? JwtKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer: issuer, audience: audience, claims: claims,
            expires: expires ?? DateTime.UtcNow.AddHours(8), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public Task<int> CreateBranchAsync() => WithDbAsync(async db =>
    {
        var branch = new SportBranch { Name = $"Test {Guid.NewGuid():N}", CreatedAt = DateTimeOffset.UtcNow };
        db.SportBranches.Add(branch);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return branch.Id;
    });

    public Task<ScheduleSlot> CreateSlotAsync(DayOfWeek day, TimeOnly start, TimeOnly end, int capacity = 20) =>
        WithDbAsync(async db =>
        {
            var slot = new ScheduleSlot
            {
                Day = day,
                StartTime = start,
                EndTime = end,
                Capacity = capacity,
                Zone = $"Zona-{Guid.NewGuid():N}",
                Enabled = true,
            };
            db.ScheduleSlots.Add(slot);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return slot;
        });

    public Task<Reservation> InsertReservationAsync(int userId, int slotId, DateOnly date,
        ReservationStatus status = ReservationStatus.Reserved) => WithDbAsync(async db =>
        {
            var reservation = new Reservation
            {
                UserId = userId,
                ScheduleSlotId = slotId,
                Date = date,
                Status = status,
                CreatedAt = DateTime.UtcNow,
            };
            db.Reservations.Add(reservation);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return reservation;
        });

    private void SetEnvironment(string name, string value)
    {
        _previousEnvironment.TryAdd(name, Environment.GetEnvironmentVariable(name));
        Environment.SetEnvironmentVariable(name, value);
    }

    private static async Task<AuthResponse> ReadAuthAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions, TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("La API devolviÃ³ una respuesta de autenticaciÃ³n vacÃ­a.");
    }

    private static string ReadSessionToken(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("__Host-gym_session=", StringComparison.Ordinal))
            .Split(';')[0].Split('=', 2)[1];

    private static void SetToken(HttpClient client, string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("La API no devolviÃ³ un token de autenticaciÃ³n.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
