namespace GymBackend.Application.Auth;

public sealed record IssuedToken(string Value, DateTimeOffset ExpiresAt);
