using GymBackend.Models;

namespace GymBackend.DTOs;

public sealed record UserResponse(int Id, string Rut, string Name, string Email, string Username, string UserType, string Role, bool Enabled)
{
    public static UserResponse From(User entity) =>
        new(entity.Id, entity.Rut, entity.Name, entity.Email, entity.Username, entity.UserType, entity.Role, entity.Enabled);
}
