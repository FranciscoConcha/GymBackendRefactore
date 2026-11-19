using System.Net;
using System.Net.Http.Json;
using GymBackend.IntegrationTests.Infrastructure;
using GymBackend.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Security;

public sealed class CsrfTests(GymApiFactory factory)
{
    private const string AllowedOrigin = "https://frontend.example";

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task UnsafeCookieRequests_RequireHeader(string method)
    {
        var admin = await factory.CreateDedicatedAdminClientAsync();
        using var client = admin.Client;
        client.DefaultRequestHeaders.Authorization = null;
        client.DefaultRequestHeaders.Remove(CsrfProtectionMiddleware.HeaderName);
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/v1/auth/logout");
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Solicitud rechazada por protección CSRF.", (await Read(response)).GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("login")]
    [InlineData("admin-login")]
    public async Task Login_RequiresHeader(string endpoint)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Remove(CsrfProtectionMiddleware.HeaderName);
        using var response = await client.PostAsJsonAsync($"/api/v1/auth/{endpoint}", new { }, JsonOptions, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(AllowedOrigin, true)]
    [InlineData("https://evil.example", false)]
    [InlineData("https://frontend.example/", false)]
    public async Task CookieRequests_CheckOrigin(string? origin, bool accepted)
    {
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = AllowedOrigin })));
        var admin = await factory.CreateDedicatedAdminClientAsync(host);
        using var client = admin.Client;
        client.DefaultRequestHeaders.Authorization = null;
        if (origin is not null) client.DefaultRequestHeaders.Add("Origin", origin);
        using var response = await client.PostAsync("/api/v1/auth/logout", null, Ct);
        Assert.Equal(accepted ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1,1")]
    [InlineData("")]
    public async Task InvalidHeader_IsRejected(string value)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Remove(CsrfProtectionMiddleware.HeaderName);
        client.DefaultRequestHeaders.TryAddWithoutValidation(CsrfProtectionMiddleware.HeaderName, value);
        using var response = await client.PostAsync("/api/v1/auth/logout", null, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void WildcardOrigin_IsRejectedAtStartup()
    {
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = "*" })));
        Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(() => host.CreateClient());
    }

    [Fact]
    public async Task BearerRequest_DoesNotRequireCsrfHeader()
    {
        var admin = await factory.CreateDedicatedAdminClientAsync();
        using var client = admin.Client;
        client.DefaultRequestHeaders.Remove(CsrfProtectionMiddleware.HeaderName);
        using var response = await client.PostAsync("/api/v1/auth/logout", null, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(AllowedOrigin, true)]
    [InlineData("https://evil.example", false)]
    public async Task Preflight_OnlyAllowsConfiguredOriginWithCredentials(string origin, bool accepted)
    {
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = AllowedOrigin })));
        using var client = host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type,x-gym-csrf");
        using var response = await client.SendAsync(request, Ct);
        if (accepted)
        {
            Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
            Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
        }
        else Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
