using System.Net;
using System.Net.Http.Json;
using GymBackend.Data;
using GymBackend.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GymBackend.IntegrationTests.Observability;

public sealed class ObservabilityTests(GymApiFactory factory)
{
    [Theory]
    [InlineData(null)]
    [InlineData("valid_ID.123-test")]
    [InlineData("contains spaces")]
    [InlineData("long")]
    public async Task CorrelationId_IsReturnedAndValidated(string? supplied)
    {
        if (supplied == "long") supplied = new string('a', 200);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        if (supplied is not null) request.Headers.TryAddWithoutValidation("X-Correlation-ID", supplied);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var id = Assert.Single(response.Headers.GetValues("X-Correlation-ID"));
        Assert.False(string.IsNullOrWhiteSpace(id));
        if (supplied == "valid_ID.123-test") Assert.Equal(supplied, id);
        else if (supplied is not null) Assert.NotEqual(supplied, id);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_ReturnsExistingContract(string path)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"status\":\"ok\"}", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Ready_UnreachableDatabase_ReturnsUnavailable()
    {
        // Program lee la cadena de conexión antes de Build(): se reconfigura el DbContext directamente.
        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Database:Initialize"] = "false" }));
            builder.ConfigureTestServices(services => services.ConfigureDbContext<AppDbContext>(options =>
                options.UseNpgsql(
                    "Host=127.0.0.1;Port=1;Database=health;Username=health;Password=health;Timeout=1;Pooling=false")));
        });
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("{\"status\":\"unavailable\"}", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(true, "127.0.0.1", HttpStatusCode.Unauthorized)]
    [InlineData(false, "127.0.0.1", HttpStatusCode.TooManyRequests)]
    [InlineData(true, "192.0.2.1", HttpStatusCode.TooManyRequests)]
    public async Task ForwardedHeaders_OnlyTrustedAndEnabled_SeparatesLoginLimits(
        bool enabled, string proxy, HttpStatusCode secondStatus)
    {
        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ForwardedHeaders:Enabled"] = enabled.ToString(),
                    ["ForwardedHeaders:KnownProxies:0"] = "127.0.0.1",
                    ["RateLimiting:Login:PermitLimit"] = "1",
                }));
            builder.ConfigureTestServices(services => services.AddSingleton<IStartupFilter>(new RemoteIpFilter(proxy)));
        });
        using var client = host.CreateClient();
        foreach (var (ip, expected) in new[]
        {
            ("203.0.113.7", HttpStatusCode.Unauthorized),
            ("203.0.113.8", secondStatus),
            ("203.0.113.7", HttpStatusCode.TooManyRequests),
        })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/admin-login")
            {
                Content = JsonContent.Create(new { username = factory.AdminUsername, password = "incorrecta" }),
            };
            request.Headers.Add("X-Forwarded-For", ip);
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(expected, response.StatusCode);
            Assert.True(response.Headers.Contains("X-Correlation-ID"));
        }
    }

    [Theory]
    [InlineData("KnownProxies", "invalid-ip")]
    [InlineData("KnownNetworks", "10.0.0.0/99")]
    public void InvalidForwardedConfiguration_FailsStartup(string key, string value)
    {
        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { [$"ForwardedHeaders:{key}:0"] = value })));
        Assert.Throws<OptionsValidationException>(() => host.CreateClient());
    }

    private sealed class RemoteIpFilter(string ip) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
                await nextMiddleware(context);
            });
            next(app);
        };
    }
}
