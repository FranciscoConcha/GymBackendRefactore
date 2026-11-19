using GymBackend.Application.Audit;
using GymBackend.Application.Common;
using GymBackend.Data;
using GymBackend.DTOs;
using GymBackend.Models;
using Microsoft.EntityFrameworkCore;
namespace GymBackend.Application.Branches;

public sealed class BranchService(AppDbContext context, IAuditLog audit)
{
    public async Task<ServiceResult<BranchResponse[]>> GetAsync(bool isAdmin, CancellationToken ct)
    {
        var branches = await context.SportBranches.AsNoTracking().Where(b => isAdmin || b.Enabled).OrderBy(b => b.Name).ToListAsync(ct);
        return ServiceResult<BranchResponse[]>.Success(branches.Select(BranchResponse.From).ToArray());
    }

    public async Task<ServiceResult<BranchMessageResponse>> UpdateAsync(int id, UpdateBranchRequest request, CancellationToken ct)
    {
        var branch = await context.SportBranches.FindAsync([id], ct);
        if (branch is null) return ServiceError.NotFound("Rama no encontrada.");
        if (request.Name is not null)
            return ServiceError.Validation("Las ramas son precargadas; solo se pueden habilitar o deshabilitar.");
        var fields = new List<string>();
        if (request.Enabled is { } enabled && branch.Enabled != enabled) fields.Add("enabled");
        branch.Enabled = request.Enabled ?? branch.Enabled;
        if (fields.Count > 0) audit.Add("branch.updated", branch, fields.ToArray());
        return await SaveAsync(branch, "Rama actualizada correctamente.", ct);
    }

    private async Task<ServiceResult<BranchMessageResponse>> SaveAsync(SportBranch branch, string message, CancellationToken ct)
    {
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        { return ServiceError.Conflict("La rama ya existe."); }
        return ServiceResult<BranchMessageResponse>.Success(new(message, BranchResponse.From(branch)));
    }
}
