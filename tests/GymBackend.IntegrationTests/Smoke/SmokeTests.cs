using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GymBackend.IntegrationTests.Infrastructure;
using GymBackend.Models;

namespace GymBackend.IntegrationTests.Smoke;

public sealed class SmokeTests(GymApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Live_ReturnsOk()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_ReturnsOk()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminLogin_WithBootstrapCredentials_ReturnsToken()
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/admin-login",
            new { username = factory.AdminUsername, password = factory.AdminPassword }, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth.Token));
    }

    [Fact]
    public async Task Schedules_WithAdmin_ReturnsSeededSlots()
    {
        using var client = await factory.CreateAdminClientAsync();
        using var response = await client.GetAsync("/api/schedules");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var slots = await response.Content.ReadFromJsonAsync<ScheduleSlot[]>(JsonOptions);
        Assert.NotNull(slots);
        Assert.True(slots.Length >= 24, "La siembra debe crear al menos 24 bloques.");
    }
}
