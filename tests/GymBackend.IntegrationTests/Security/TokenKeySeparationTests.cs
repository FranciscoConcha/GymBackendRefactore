using System.Net;
using GymBackend.Application.Qr;
using GymBackend.Extensions;
using GymBackend.IntegrationTests.Infrastructure;
using GymBackend.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Security;

public sealed class TokenKeySeparationTests(GymApiFactory factory)
{
    [Theory]
    [InlineData("incorrect", "gym-tests-clients")]
    [InlineData("gym-tests", "incorrect")]
    [InlineData(null, "gym-tests-clients")]
    [InlineData("gym-tests", null)]
    public async Task Jwt_InvalidIssuerOrAudience_IsUnauthorized(string? issuer, string? audience)
    {
        var user = await factory.CreateClientUserAsync();
        using var original = user.Client;
        using var client = factory.CreateClientWithToken(factory.CreateJwt(
            user.UserId.ToString(), "Usuario", user.Rut, issuer: issuer, audience: audience));
        using var response = await client.GetAsync("/api/v1/me/reservations/1/qr", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task QrAndJwt_CannotBeUsedInterchangeably()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        var today = factory.Services.GetRequiredService<IGymClock>().Today;
        var slot = await factory.CreateSlotAsync(today.DayOfWeek, new(10, 0), new(11, 0));
        var reservation = await factory.InsertReservationAsync(user.UserId, slot.Id, today);
        var qr = await Api.Qr(client, reservation.Id);
        Assert.True(AccessQrToken.TryRead(qr, factory.QrKey, out var payload));
        Assert.Equal(user.UserId, payload.UserId);
        using var qrBearer = factory.CreateClientWithToken(qr);
        using var denied = await qrBearer.GetAsync("/api/v1/me/reservations/1/qr", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using var wrongKey = factory.CreateClientWithToken(factory.CreateJwt(
            user.UserId.ToString(), "Usuario", user.Rut, signingKey: factory.QrKey));
        using var wrongKeyResponse = await wrongKey.GetAsync("/api/v1/me/reservations/1/qr", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongKeyResponse.StatusCode);
        using var admin = await factory.CreateAdminClientAsync();
        foreach (var action in new[] { "validate", "entry" })
        {
            await Expect(await Api.Access(admin, action, AccessQrToken.Create(payload, factory.JwtKey)),
                HttpStatusCode.BadRequest, "Código QR inválido.");
            await Expect(await Api.Access(admin, action, client.DefaultRequestHeaders.Authorization!.Parameter!),
                HttpStatusCode.BadRequest, "Código QR inválido.");
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("jwt-signing-key-at-least-32-characters")]
    public void Qr_InvalidSigningKey_FailsStartupValidation(string? signingKey)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "jwt-signing-key-at-least-32-characters",
                ["Jwt:Issuer"] = "tests",
                ["Jwt:Audience"] = "clients",
                ["Qr:SigningKey"] = signingKey,
            }).Build());
        services.AddGymOptions();
        using var provider = services.BuildServiceProvider();
        var error = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Equal("QrOptions", error.OptionsType.Name);
        Assert.Contains(error.Failures, failure => failure.Contains("Qr:SigningKey"));
    }
}
