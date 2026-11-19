using GymBackend.Application.Branches;
using GymBackend.Domain;
using GymBackend.DTOs;
using GymBackend.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace GymBackend.Controllers;

[ApiController]
[Route(ApiRoutes.V1)]
[Authorize]
public sealed class BranchesController(BranchService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        this.ToActionResult(await service.GetAsync(User.IsInRole(Roles.Admin), ct));

    [HttpPatch("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Update(int id, UpdateBranchRequest request, CancellationToken ct) =>
        this.ToActionResult(await service.UpdateAsync(id, request, ct));
}
