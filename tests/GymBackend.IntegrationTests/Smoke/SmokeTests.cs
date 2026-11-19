using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GymBackend.IntegrationTests.Infrastructure;
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
    public async Task AdminLogin_WithBootstrapCredentials_ReturnsSession()
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/admin-login",
            new { username = factory.AdminUsername, password = factory.AdminPassword }, JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.NotNull(auth);
        Assert.True(auth.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.True(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Schedules_WithAdmin_ReturnsOwnSlot()
    {
        var date = GymTime.Tomorrow();
        using var client = await factory.CreateAdminClientAsync();
        var own = await factory.CreateSlotAsync(date.DayOfWeek, new TimeOnly(8, 0), new TimeOnly(9, 0));
        using var response = await client.GetAsync($"/api/v1/schedules?date={date:yyyy-MM-dd}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var slots = await response.Content.ReadFromJsonAsync<GymBackend.DTOs.ScheduleResponse[]>(JsonOptions, TestContext.Current.CancellationToken);
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
                .Where(slot => slot.Zone == "Sala de Pesas" && slot.Capacity == 30)
                .ToListAsync(ct);
            var adminUser = await db.Users.SingleAsync(user => user.Rut == factory.AdminRut, ct);
            Assert.All(seeded, slot =>
            {
                Assert.True(slot.Enabled);
                Assert.True(slot.EndTime > slot.StartTime);
            });
            return (seeded.Count, seeded.Select(slot => slot.Day).Distinct().OrderBy(day => day).ToArray(), adminUser);
        });
        Assert.Equal(24, seededSlots);
        Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday }, seededDays);
        Assert.Equal("Admin", admin.Role);
        Assert.Null(admin.Category);
        Assert.Equal(factory.AdminUsername.ToLowerInvariant(), admin.Username);
    }
}
