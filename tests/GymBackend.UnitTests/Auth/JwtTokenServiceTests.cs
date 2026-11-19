using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GymBackend.Application.Auth;
using GymBackend.Domain;
using GymBackend.Models;
using GymBackend.Options;
using Microsoft.IdentityModel.Tokens;

namespace GymBackend.UnitTests.Auth;

public sealed class JwtTokenServiceTests
{
    [Theory]
    [InlineData(Roles.Usuario)]
    [InlineData(Roles.Admin)]
    public void CreateToken_PreservesClaimsAlgorithmAndEightHourExpiry(string role)
    {
        var service = new JwtTokenService(Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            Issuer = "unit-issuer",
            Audience = "unit-audience",
            Key = "unit-test-signing-key-at-least-32-bytes-long",
        }));
        var user = new User
        {
            Id = 42,
            FirstName = "Test",
            LastName = "User",
            Role = role,
            Rut = "123456789",
            Email = "user@example.test",
            SessionVersion = 7,
        };
        var before = DateTime.UtcNow;
        var encoded = service.CreateToken(user);
        var after = DateTime.UtcNow;
        var token = new JwtSecurityTokenHandler().ReadJwtToken(encoded.Value);

        Assert.Equal(token.ValidTo, encoded.ExpiresAt.UtcDateTime);
        Assert.Equal("42", token.Claims.Single(claim => claim.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal(user.FullName, token.Claims.Single(claim => claim.Type == ClaimTypes.Name).Value);
        Assert.Equal(role, token.Claims.Single(claim => claim.Type == ClaimTypes.Role).Value);
        Assert.Equal(user.Rut, token.Claims.Single(claim => claim.Type == GymClaimTypes.Rut).Value);
        Assert.Equal(user.Email, token.Claims.Single(claim => claim.Type == GymClaimTypes.Email).Value);
        Assert.Equal("7", token.Claims.Single(claim => claim.Type == "sv").Value);
        Assert.Equal("unit-issuer", token.Issuer);
        Assert.Equal("unit-audience", Assert.Single(token.Audiences));
        Assert.Equal(SecurityAlgorithms.HmacSha256, token.Header.Alg);
        Assert.InRange(token.ValidTo, before.AddHours(8).AddSeconds(-1), after.AddHours(8));
    }
}
