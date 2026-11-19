using GymBackend.DTOs;

namespace GymBackend.Application.Auth;

public sealed record AuthSession(string Message, IssuedToken Token, UserResponse User);
