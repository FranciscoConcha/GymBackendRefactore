using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GymBackend.Data;
using GymBackend.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Time.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using GymBackend.Domain;
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
            // Umbral alto: las pruebas inician sesión muchas veces; las de límite usan un host derivado.
            SetEnvironment("RateLimiting__Login__PermitLimit", "100000");
            SetEnvironment("Database__Initialize", "true");
            SetEnvironment("Bootstrap__AdminRut", AdminRut);
            SetEnvironment("Bootstrap__AdminEmail", AdminEmail);
            SetEnvironment("Bootstrap__AdminUsername", AdminUsername);
            SetEnvironment("Bootstrap__AdminPassword", AdminPassword);
            _ = Server;
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
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

    public async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = CreateClient();
        try
        {
            using var response = await client.PostAsJsonAsync("/api/auth/admin-login",
                new { username = AdminUsername, password = AdminPassword }, JsonOptions, TestContext.Current.CancellationToken);
            var auth = await ReadAuthAsync(response);
            SetToken(client, auth.Token);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public async Task<(HttpClient Client, int UserId, string Rut, string Password)> CreateClientUserAsync(
        string userType = "Estudiante")
    {
        var rut = TestData.NextRut();
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        using var admin = await CreateAdminClientAsync();
        using var registration = await admin.PostAsJsonAsync("/api/auth/register", new
        {
            rut, name = $"Test {rut}", email = $"user-{rut}@example.test", password, userType,
        }, JsonOptions, TestContext.Current.CancellationToken);
        var registered = await ReadAuthAsync(registration);
        var userId = registered.User?.Id
            ?? throw new InvalidOperationException("El registro no devolvió el usuario.");
        var client = CreateClient();
        try
        {
            using var login = await client.PostAsJsonAsync("/api/auth/login", new { rut, password }, JsonOptions, TestContext.Current.CancellationToken);
            var auth = await ReadAuthAsync(login);
            SetToken(client, auth.Token);
            return (client, userId, rut, password);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public HttpClient CreateClientWithToken(string token)
    {
        var client = CreateClient();
        SetToken(client, token);
        return client;
    }

    public string CreateJwt(int userId, string role, string rut, string name = "Test", DateTime? expires = null)
        => CreateJwt(userId.ToString(CultureInfo.InvariantCulture), role, rut, name, expires);

    public string CreateJwt(string? userId, string role, string rut, string name = "Test", DateTime? expires = null,
        string? issuer = "gym-tests", string? audience = "gym-tests-clients", string? signingKey = null)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, name),
            new Claim(ClaimTypes.Role, role),
            new Claim("rut", rut),
            new Claim("email", $"user-{rut}@example.test"),
        };
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

    public Task<ScheduleSlot> CreateSlotAsync(DayOfWeek day, TimeOnly start, TimeOnly end, int capacity = 20) =>
        WithDbAsync(async db =>
        {
            var slot = new ScheduleSlot
            {
                Day = day, StartTime = start, EndTime = end, Capacity = capacity,
                Zone = $"Zona-{Guid.NewGuid():N}", Enabled = true,
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
                UserId = userId, ScheduleSlotId = slotId, Date = date,
                Status = status, CreatedAt = DateTime.UtcNow,
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
            ?? throw new InvalidOperationException("La API devolvió una respuesta de autenticación vacía.");
    }

    private static void SetToken(HttpClient client, string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("La API no devolvió un token de autenticación.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
