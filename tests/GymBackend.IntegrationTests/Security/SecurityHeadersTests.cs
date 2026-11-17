using System.Net;
using GymBackend.IntegrationTests.Infrastructure;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Security;

public sealed class SecurityHeadersTests(GymApiFactory factory)
{
    [Theory]
    [InlineData("/health/live", HttpStatusCode.OK)]
    [InlineData("/api/schedules", HttpStatusCode.Unauthorized)]
    [InlineData("/api/does-not-exist", HttpStatusCode.NotFound)]
    public async Task AnonymousResponses_HaveSecurityHeaders(string path, HttpStatusCode status)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path, Ct);
        Assert.Equal(status, response.StatusCode);
        AssertHeaders(response);
        if (path.StartsWith("/api/", StringComparison.Ordinal)) AssertNoStore(response);
    }

    [Fact]
    public async Task ForbiddenResponse_HasSecurityHeaders()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        using var response = await client.GetAsync("/api/auth/users", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        AssertHeaders(response);
        AssertNoStore(response);
    }

    [Fact]
    public async Task SuccessfulApiResponse_HasSecurityHeadersAndNoStore()
    {
        using var admin = await factory.CreateAdminClientAsync();
        using var response = await admin.GetAsync("/api/auth/users", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertHeaders(response);
        AssertNoStore(response);
    }

    [Fact]
    public async Task Swagger_DoesNotHaveRestrictiveCsp()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/swagger/index.html", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Content-Security-Policy"));
        AssertHeaders(response, expectCsp: false);
    }

    private static void AssertNoStore(HttpResponseMessage response) =>
        Assert.True(response.Headers.CacheControl?.NoStore == true);

    private static void AssertHeaders(HttpResponseMessage response, bool expectCsp = true)
    {
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        if (expectCsp)
            Assert.Equal("default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'",
                Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal("same-origin", Assert.Single(response.Headers.GetValues("Cross-Origin-Opener-Policy")));
        Assert.Equal("camera=(), microphone=(), geolocation=(), payment=()",
            Assert.Single(response.Headers.GetValues("Permissions-Policy")));
    }
}
