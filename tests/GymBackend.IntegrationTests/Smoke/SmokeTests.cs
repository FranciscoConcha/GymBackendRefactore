using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GymBackend.IntegrationTests.Infrastructure;
using GymBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace GymBackend.IntegrationTests.Smoke;

public sealed class SmokeTests(GymApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Live_ReturnsOk()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_ReturnsOk()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminLogin_WithBootstrapCredentials_ReturnsToken()
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/admin-login",
            new { username = factory.AdminUsername, password = factory.AdminPassword }, JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth.Token));
    }

    [Fact]
    public async Task Schedules_WithAdmin_ReturnsOwnSlot()
    {
        using var client = await factory.CreateAdminClientAsync();
        var own = await factory.CreateSlotAsync("Lunes", "08:00", "09:00");
        using var response = await client.GetAsync("/api/schedules", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var slots = await response.Content.ReadFromJsonAsync<ScheduleSlot[]>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.NotNull(slots);
        Assert.Contains(slots, slot => slot.Id == own.Id);
    }

    [Fact]
    public async Task DevelopmentSeed_CreatesBaseScheduleAndAdmin()
    {
        var ct = TestContext.Current.CancellationToken;
        var (seededSlots, seededDays, admin) = await factory.WithDbAsync(async db =>
        {
            var seeded = await db.ScheduleSlots
                .Where(slot => slot.Zone == "Sala de Pesas" && slot.Capacity == 20)
                .ToListAsync(ct);
            var adminUser = await db.Users.SingleAsync(user => user.Rut == factory.AdminRut, ct);
            return (seeded.Count, seeded.Select(slot => slot.Day).Distinct().Count(), adminUser);
        });
        Assert.Equal(24, seededSlots);
        Assert.Equal(6, seededDays);
        Assert.Equal("Admin", admin.Role);
        Assert.Equal("Administrador", admin.UserType);
        Assert.Equal(factory.AdminUsername.ToLowerInvariant(), admin.Username);
    }
}
