using System.Net;
using System.Net.Http.Json;
using GymBackend.IntegrationTests.Infrastructure;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Auth;

public sealed class InvalidUserClaimsTests(GymApiFactory factory)
{
    [Theory]
    [InlineData("abc")]
    [InlineData(null)]
    public async Task MissingOrInvalidUserId_ReturnsUnauthorized(string? userId)
    {
        using var client = factory.CreateClientWithToken(factory.CreateJwt(userId, "Usuario", TestData.NextRut()));
        using var qr = await client.GetAsync("/api/v1/qr/my-code", Ct);
        using var reservation = await client.PostAsJsonAsync("/api/v1/reservations",
            new { scheduleSlotId = 0, date = GymTime.Tomorrow() }, JsonOptions, Ct);
        using var own = await client.GetAsync("/api/v1/reservations/user/1", Ct);
        using var schedules = await client.GetAsync("/api/v1/schedules", Ct);
        foreach (var response in new[] { qr, reservation, own, schedules })
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
