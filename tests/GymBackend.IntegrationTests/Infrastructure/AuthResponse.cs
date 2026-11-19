namespace GymBackend.IntegrationTests.Infrastructure;

public sealed record AuthResponse(string? Message, UserDto? User, DateTimeOffset ExpiresAt);

public sealed record UserDto(int Id, string Rut, string FirstName, string LastName, string FullName, string Email,
    string Username, string? Category, string Role, GymBackend.DTOs.UserBranchResponse? SportBranch,
    bool Enabled, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
