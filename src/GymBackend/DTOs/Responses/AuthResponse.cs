namespace GymBackend.DTOs;

public sealed record AuthResponse(string Message, string Token, UserResponse User);
