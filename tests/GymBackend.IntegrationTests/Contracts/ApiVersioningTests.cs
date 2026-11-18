using System.Net;
using System.Net.Http.Json;
using GymBackend.IntegrationTests.Infrastructure;

namespace GymBackend.IntegrationTests.Contracts;

public sealed class ApiVersioningTests(GymApiFactory factory)
{
    [Fact]
    public async Task LegacyApiRoutes_ReturnNotFound_AndV1RouteWorks()
    {
        using var anonymous = factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        using var schedules = await anonymous.GetAsync("/api/schedules", ct);
        Assert.Equal(HttpStatusCode.NotFound, schedules.StatusCode);

        using var login = await anonymous.PostAsJsonAsync("/api/auth/login",
            new { rut = "legacy", password = "legacy" }, ct);
        Assert.Equal(HttpStatusCode.NotFound, login.StatusCode);

        using var qr = await anonymous.GetAsync("/api/qr/my-code", ct);
        Assert.Equal(HttpStatusCode.NotFound, qr.StatusCode);

        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        using var versionedSchedules = await client.GetAsync("/api/v1/schedules", ct);
        Assert.Equal(HttpStatusCode.OK, versionedSchedules.StatusCode);
    }
}
