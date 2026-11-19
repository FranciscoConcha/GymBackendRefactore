using GymBackend.Application.Audit;
using GymBackend.Application.Auth;
using GymBackend.Application.Common;
using GymBackend.Data;
using GymBackend.DTOs;
using GymBackend.Models;
using GymBackend.Time;
using Microsoft.EntityFrameworkCore;
namespace GymBackend.Application.Branches;

public sealed class BranchService(AppDbContext context, IGymClock clock, IAuditLog audit)
{
    public async Task<ServiceResult<BranchResponse[]>> GetAsync(bool isAdmin, CancellationToken ct)
    {
        var branches = await context.SportBranches.AsNoTracking().Where(b => isAdmin || b.Enabled).OrderBy(b => b.Name).ToListAsync(ct);
        return ServiceResult<BranchResponse[]>.Success(branches.Select(BranchResponse.From).ToArray());
    }

    public async Task<ServiceResult<BranchMessageResponse>> CreateAsync(CreateBranchRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        if (!ValidName(name)) return ServiceError.Validation("El nombre de rama debe ser v\u00e1lido y tener hasta 80 caracteres.");
        if (await DuplicateAsync(name, 0, ct)) return ServiceError.Conflict("La rama ya existe.");
        var branch = new SportBranch { Name = name, CreatedAt = clock.UtcNow };
        context.SportBranches.Add(branch);
        audit.Add("branch.created", branch);
        return await SaveAsync(branch, "Rama creada correctamente.", ct);
    }

    public async Task<ServiceResult<BranchMessageResponse>> UpdateAsync(int id, UpdateBranchRequest request, CancellationToken ct)
    {
        var branch = await context.SportBranches.FindAsync([id], ct);
        if (branch is null) return ServiceError.NotFound("Rama no encontrada.");
        var name = request.Name?.Trim() ?? branch.Name;
        if (!ValidName(name)) return ServiceError.Validation("El nombre de rama debe ser v\u00e1lido y tener hasta 80 caracteres.");
        if (await DuplicateAsync(name, id, ct)) return ServiceError.Conflict("La rama ya existe.");
        var fields = new List<string>();
        if (branch.Name != name) fields.Add("name");
        if (request.Enabled is { } enabled && branch.Enabled != enabled) fields.Add("enabled");
        branch.Name = name;
        branch.Enabled = request.Enabled ?? branch.Enabled;
        if (fields.Count > 0) audit.Add("branch.updated", branch, fields.ToArray());
        return await SaveAsync(branch, "Rama actualizada correctamente.", ct);
    }

    private static bool ValidName(string name) => name.Length <= 80 && InputRules.IsSafeName(name);
    private Task<bool> DuplicateAsync(string name, int id, CancellationToken ct) =>
        context.SportBranches.AnyAsync(b => b.Id != id && b.Name.ToLower() == name.ToLower(), ct);

    private async Task<ServiceResult<BranchMessageResponse>> SaveAsync(SportBranch branch, string message, CancellationToken ct)
    {
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        { return ServiceError.Conflict("La rama ya existe."); }
        return ServiceResult<BranchMessageResponse>.Success(new(message, BranchResponse.From(branch)));
    }
}
