namespace GymBackend.Application.Qr;

public interface IQrTokenService
{
    string Generate(string rut, DateOnly date);
    bool TryValidate(string? token, DateOnly today, out string rut, out string error);
}
