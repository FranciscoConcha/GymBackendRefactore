namespace GymBackend.DTOs;

public sealed class UpdateUserRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Username { get; set; }
    public string? Role { get; set; }
    private string? _category;
    private int? _sportBranchId;
    public string? Category { get => _category; set { _category = value; HasCategory = true; } }
    public int? SportBranchId { get => _sportBranchId; set { _sportBranchId = value; HasSportBranchId = true; } }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasCategory { get; private set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasSportBranchId { get; private set; }
}
