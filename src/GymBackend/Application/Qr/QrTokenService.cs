using GymBackend.Options;
using Microsoft.Extensions.Options;

namespace GymBackend.Application.Qr;

public sealed class QrTokenService(IOptions<QrOptions> options) : IQrTokenService
{
    public string Generate(AccessQrPayload payload) => AccessQrToken.Create(payload, options.Value.SigningKey!);
    public bool TryRead(string? token, out AccessQrPayload payload) => AccessQrToken.TryRead(token, options.Value.SigningKey!, out payload);
}
