using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GymBackend.Domain;
using GymBackend.Models;
using GymBackend.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GymBackend.Application.Auth;

public sealed class JwtTokenService(IOptions<JwtOptions> jwtOptions, TimeProvider? timeProvider = null) : ITokenService
{
    public IssuedToken CreateToken(User user)
    {
        var key = jwtOptions.Value.Key
            ?? throw new InvalidOperationException("Jwt:Key no está configurada.");

        var claims = new[]
        {
            new Claim("sv", user.SessionVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(GymClaimTypes.Rut, user.Rut),
            new Claim(GymClaimTypes.Email, user.Email),
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var expires = DateTimeOffset.FromUnixTimeSeconds((timeProvider ?? TimeProvider.System).GetUtcNow().AddHours(8).ToUnixTimeSeconds());
        var token = new JwtSecurityToken(
            issuer: jwtOptions.Value.Issuer,
            audience: jwtOptions.Value.Audience,
            claims: claims,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
