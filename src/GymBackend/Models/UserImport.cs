namespace GymBackend.Models;

public sealed class UserImport
{
    public int Id { get; set; }
    public int CreatedById { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string Status { get; set; } = "Pendiente";
    public string PayloadJson { get; set; } = "[]";
    public string? ResultJson { get; set; }
}
