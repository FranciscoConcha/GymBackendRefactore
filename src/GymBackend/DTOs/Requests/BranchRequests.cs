namespace GymBackend.DTOs;

public sealed class UpdateBranchRequest
{
    public string? Name { get; set; }
    public bool? Enabled { get; set; }
}
