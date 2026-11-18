using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GymBackend.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Security;

public sealed class LoginRateLimitTests(GymApiFactory factory)
{
    private const string RejectionMessage = "Demasiados intentos de inicio de sesión. Intente nuevamente más tarde.";

    [Fact]
    public async Task Login_RejectsRequestsAfterPermitLimit()
    {
        using var host = CreateHost();
        using var client = host.CreateClient();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var response = await client.PostAsJsonAsync("/api/auth/login",
                new { rut = factory.AdminRut, password = "incorrecta" }, JsonOptions, Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        await AssertRateLimited(await client.PostAsJsonAsync("/api/auth/login",
            new { rut = factory.AdminRut, password = "incorrecta" }, JsonOptions, Ct));
        using var health = await client.GetAsync("/health/live", Ct);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task AdminLogin_RejectsRequestsAfterPermitLimit()
    {
        using var host = CreateHost();
        using var client = host.CreateClient();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var response = await client.PostAsJsonAsync("/api/auth/admin-login",
                new { username = factory.AdminUsername, password = "incorrecta" }, JsonOptions, Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        await AssertRateLimited(await client.PostAsJsonAsync("/api/auth/admin-login",
            new { username = factory.AdminUsername, password = "incorrecta" }, JsonOptions, Ct));
    }

    private WebApplicationFactory<Program> CreateHost() => factory.WithWebHostBuilder(builder =>
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["RateLimiting:Login:PermitLimit"] = "3",
                ["RateLimiting:Login:WindowSeconds"] = "60",
            })));

    private static async Task AssertRateLimited(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
            Assert.NotNull(response.Headers.RetryAfter);
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(Ct), cancellationToken: Ct);
            Assert.Equal(RejectionMessage, json.RootElement.GetProperty("message").GetString());
        }
    }
}
