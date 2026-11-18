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
            user.UserId.ToString(), "Cliente", user.Rut, issuer: issuer, audience: audience));
        using var response = await client.GetAsync("/api/v1/qr/my-code", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task QrAndJwt_CannotBeUsedInterchangeably()
    {
        var user = await factory.CreateClientUserAsync();
        using var client = user.Client;
        var qr = await Api.Qr(client);
        var today = factory.Services.GetRequiredService<IGymClock>().Today;
        Assert.True(QrTokenFormat.TryValidate(qr, factory.QrKey, today, out var rut, out _));
        Assert.Equal(user.Rut, rut);
        using var qrBearer = factory.CreateClientWithToken(qr);
        using var denied = await qrBearer.GetAsync("/api/v1/qr/my-code", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using var wrongKey = factory.CreateClientWithToken(factory.CreateJwt(
            user.UserId.ToString(), "Cliente", user.Rut, signingKey: factory.QrKey));
        using var wrongKeyResponse = await wrongKey.GetAsync("/api/v1/qr/my-code", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongKeyResponse.StatusCode);
        using var admin = await factory.CreateAdminClientAsync();
        foreach (var action in new[] { "entry", "exit" })
        {
            await Expect(await Api.Attendance(admin, action, QrTokenFormat.Create(user.Rut, factory.JwtKey, today)),
                HttpStatusCode.BadRequest, "Código QR inválido.");
            await Expect(await Api.Attendance(admin, action, client.DefaultRequestHeaders.Authorization!.Parameter!),
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
