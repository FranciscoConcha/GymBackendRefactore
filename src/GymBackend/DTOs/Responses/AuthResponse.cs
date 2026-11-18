namespace GymBackend.DTOs;

public sealed record AuthResponse(string Message, UserResponse User, DateTimeOffset ExpiresAt);
