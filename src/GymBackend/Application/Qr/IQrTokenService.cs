namespace GymBackend.Application.Qr;

public interface IQrTokenService
{
    string Generate(AccessQrPayload payload);
    bool TryRead(string? token, out AccessQrPayload payload);
}
