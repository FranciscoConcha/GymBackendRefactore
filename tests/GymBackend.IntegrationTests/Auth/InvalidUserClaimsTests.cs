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
        using var client = factory.CreateClientWithToken(factory.CreateJwt(userId, "Cliente", TestData.NextRut()));
        const string message = "La sesión no contiene un usuario válido.";

        await Expect(await client.GetAsync("/api/qr/my-code", Ct), HttpStatusCode.Unauthorized, message);
        await Expect(await client.PostAsJsonAsync("/api/reservations",
            new { day = "Lunes", startTime = "08:00", endTime = "09:00" }, JsonOptions, Ct),
            HttpStatusCode.Unauthorized, message);
        await Expect(await client.GetAsync("/api/reservations/user/1", Ct), HttpStatusCode.Unauthorized, message);
    }
}
