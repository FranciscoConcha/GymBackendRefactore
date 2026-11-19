using GymBackend.Domain;
namespace GymBackend.DTOs;

public class RegisterRequest
{
    public string Rut { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Password { get; set; }

    public string Role { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int? SportBranchId { get; set; }
    public string? Username { get; set; }
}
