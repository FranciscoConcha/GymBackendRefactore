using GymBackend.Application.Access;
using GymBackend.Domain;
using GymBackend.Extensions;
using GymBackend.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymBackend.Controllers;

[ApiController]
[RequestValidation]
[Authorize]
public sealed class VisitsController(AccessService service) : ControllerBase
{
    [HttpGet("api/v1/visits")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> All(DateOnly? from, DateOnly? to, int? userId, int? branchId, string? category, CancellationToken ct) =>
        Ok(await service.HistoryAsync(from, to, userId, branchId, category, ct));

    [HttpGet("api/v1/me/visits")]
    public async Task<IActionResult> Mine(CancellationToken ct) =>
        Ok(await service.HistoryAsync(null, null, User.GetUserId()!.Value, null, null, ct));
}
