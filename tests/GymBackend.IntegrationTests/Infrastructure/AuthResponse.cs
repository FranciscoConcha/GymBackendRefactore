namespace GymBackend.IntegrationTests.Infrastructure;

public sealed record AuthResponse(string? Message, string? Token, UserDto? User);

public sealed record UserDto(int Id, string Rut, string Name, string Email,
    string Username, string UserType, string Role, bool Enabled);
