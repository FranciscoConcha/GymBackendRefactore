using GymBackend.Domain;
using GymBackend.Models;
using GymBackend.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace GymBackend.Application.Auth;

public sealed class JwtTokenService(IOptions<JwtOptions> jwtOptions) : ITokenService
{
    public string CreateToken(User user)
    {
        var key = jwtOptions.Value.Key
            ?? throw new InvalidOperationException("Jwt:Key no está configurada.");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(GymClaimTypes.Rut, user.Rut),
            new Claim(GymClaimTypes.Email, user.Email),
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
