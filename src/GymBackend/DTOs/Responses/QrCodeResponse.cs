namespace GymBackend.DTOs;

public sealed record QrCodeResponse(string Rut, string Name, string Date, string Token, DateTimeOffset ValidFrom, DateTimeOffset ValidUntil);
