using GymBackend.Models;
namespace GymBackend.DTOs;

public sealed record UserBranchResponse(int Id, string Name);
public sealed record UserResponse(int Id, string Rut, string FirstName, string LastName, string FullName,
    string Email, string Username, string Role, string? Category, UserBranchResponse? SportBranch,
    bool Enabled, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, bool MustChangePassword)
{
    public static UserResponse From(User entity) =>
        new(entity.Id, entity.Rut, entity.FirstName, entity.LastName, entity.FullName, entity.Email,
            entity.Username, entity.Role, entity.Category,
            entity.SportBranch is { } branch ? new(branch.Id, branch.Name) : null,
            entity.Enabled, entity.CreatedAt, entity.UpdatedAt, entity.MustChangePassword);
}
