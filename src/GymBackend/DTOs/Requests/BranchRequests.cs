namespace GymBackend.DTOs;

public sealed class CreateBranchRequest
{
    public string Name { get; set; } = string.Empty;
}
public sealed class UpdateBranchRequest
{
    public string? Name { get; set; }
    public bool? Enabled { get; set; }
}
