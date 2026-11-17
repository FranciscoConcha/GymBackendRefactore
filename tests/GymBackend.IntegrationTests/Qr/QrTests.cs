using System.Globalization;
using System.Net;
using GymBackend.IntegrationTests.Infrastructure;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Qr;

public sealed class QrTests(GymApiFactory factory)
{
    [Fact]
    public async Task MyCode_PermisosYContenido_ComportamientoActual()
    {
        using var anonymous = factory.CreateClient();
        using var denied = await anonymous.GetAsync("/api/qr/my-code", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using var ghost = factory.CreateClientWithToken(factory.CreateJwt(999999, "Cliente", TestData.NextRut()));
        await Expect(await ghost.GetAsync("/api/qr/my-code", Ct), HttpStatusCode.Unauthorized, "La sesión no corresponde a un usuario válido.");
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        var before = GymTime.Today();
        var json = await Expect(await client.GetAsync("/api/qr/my-code", Ct), HttpStatusCode.OK);
        var after = GymTime.Today();
        Assert.Equal(user.Rut, json.GetProperty("rut").GetString());
        Assert.Equal($"Test {user.Rut}", json.GetProperty("name").GetString());
        Assert.Contains(json.GetProperty("date").GetString(), new[] { before.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), after.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("token").GetString()));
    }
}
