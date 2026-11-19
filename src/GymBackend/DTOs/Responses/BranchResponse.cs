using GymBackend.Models;
namespace GymBackend.DTOs;

public sealed record BranchResponse(int Id, string Name, bool Enabled)
{
    public static BranchResponse From(SportBranch branch) => new(branch.Id, branch.Name, branch.Enabled);
}
public sealed record BranchMessageResponse(string Message, BranchResponse Branch);
