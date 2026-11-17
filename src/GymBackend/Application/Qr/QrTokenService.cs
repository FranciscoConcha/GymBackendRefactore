using GymBackend.Options;
using Microsoft.Extensions.Options;

namespace GymBackend.Application.Qr;

public sealed class QrTokenService(IOptions<JwtOptions> jwtOptions) : IQrTokenService
{
    private string SigningKey => jwtOptions.Value.Key
        ?? throw new InvalidOperationException("Jwt:Key no está configurada.");

    public string Generate(string rut, DateOnly date) => QrTokenFormat.Create(rut, SigningKey, date);

    public bool TryValidate(string? token, DateOnly today, out string rut, out string error) =>
        QrTokenFormat.TryValidate(token, SigningKey, today, out rut, out error);
}
