using GymBackend.Domain;
namespace GymBackend.Models;

public class User
{
    public int Id { get; set; }

    public string Rut { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";

    public string Email { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public AccountStatus AccountStatus { get; set; } = AccountStatus.Activa;

    public bool MustChangePassword { get; set; }
    public int SessionVersion { get; set; }

    public string? Category { get; set; }
    public int? SportBranchId { get; set; }
    public SportBranch? SportBranch { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public string Role { get; set; } = Roles.Usuario;

    public bool Enabled { get; set; } = true;
}
