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
    [InlineData(Roles.Client)]
    [InlineData(Roles.Admin)]
    public void CreateToken_PreservesClaimsAlgorithmAndEightHourExpiry(string role)
    {
        var service = new JwtTokenService(Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            Key = "unit-test-signing-key-at-least-32-bytes-long",
        }));
        var user = new User
        {
            Id = 42,
            Name = "Test User",
            Role = role,
            Rut = "123456789",
            Email = "user@example.test",
        };
        var before = DateTime.UtcNow;
        var encoded = service.CreateToken(user);
        var after = DateTime.UtcNow;
        var token = new JwtSecurityTokenHandler().ReadJwtToken(encoded);

        Assert.Equal("42", token.Claims.Single(claim => claim.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal(user.Name, token.Claims.Single(claim => claim.Type == ClaimTypes.Name).Value);
        Assert.Equal(role, token.Claims.Single(claim => claim.Type == ClaimTypes.Role).Value);
        Assert.Equal(user.Rut, token.Claims.Single(claim => claim.Type == GymClaimTypes.Rut).Value);
        Assert.Equal(user.Email, token.Claims.Single(claim => claim.Type == GymClaimTypes.Email).Value);
        Assert.Equal(SecurityAlgorithms.HmacSha256, token.Header.Alg);
        Assert.InRange(token.ValidTo, before.AddHours(8).AddSeconds(-1), after.AddHours(8));
    }
}
