using System.Net;
using GymBackend.IntegrationTests.Infrastructure;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Security;

public sealed class DisabledUserTokenTests(GymApiFactory factory)
{
    [Fact]
    public async Task PreviouslyIssuedToken_IsRejectedAfterDisablingUser()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        using var admin = await factory.CreateAdminClientAsync();
        using (var allowed = await client.GetAsync("/api/v1/schedules", Ct)) Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        await Expect(await Status(admin, user.UserId, false), HttpStatusCode.OK);
        using var denied = await client.GetAsync("/api/v1/me/reservations/1/qr", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        await Expect(await Status(admin, user.UserId, true), HttpStatusCode.OK);
        using (var allowed = await client.GetAsync("/api/v1/schedules", Ct)) Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }
}
