using System.Net;
using System.Net.Http.Json;
using GymBackend.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Security;

public sealed class AccountRateLimitTests(GymApiFactory factory)
{
    [Theory]
    [InlineData("activate")]
    [InlineData("password-resets")]
    [InlineData("password-resets/confirm")]
    public async Task PublicAccountEndpoint_IsRateLimited(string endpoint)
    {
        using var host = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
            new Dictionary<string, string?> { ["RateLimiting:Login:PermitLimit"] = "3", ["RateLimiting:Login:WindowSeconds"] = "60" })));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Remove("X-Gym-Csrf");
        client.DefaultRequestHeaders.Add("X-Gym-Csrf", "1");
        var body = new { email = $"missing-{Guid.NewGuid():N}@example.test", token = "invalid", newPassword = "ValidPassword123" };
        for (var i = 0; i < 3; i++)
            await Expect(await client.PostAsJsonAsync($"/api/v1/auth/{endpoint}", body, JsonOptions, Ct),
                endpoint == "password-resets" ? HttpStatusCode.Accepted : HttpStatusCode.BadRequest);
        using var limited = await client.PostAsJsonAsync($"/api/v1/auth/{endpoint}", body, JsonOptions, Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }
}
