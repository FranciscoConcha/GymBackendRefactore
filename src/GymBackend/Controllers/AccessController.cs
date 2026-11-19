using GymBackend.Application.Access;
using GymBackend.Domain;
using GymBackend.DTOs;
using GymBackend.Extensions;
using GymBackend.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GymBackend.Controllers;

[ApiController]
[RequestValidation]
[Route("api/v1/access")]
[Authorize(Roles = Roles.Admin + "," + Roles.Recepcionista)]
[EnableRateLimiting("access")]
[ServiceFilter(typeof(AccessAttemptFilter), Order = -3000)]
public sealed class AccessController(AccessService service) : ControllerBase
{
    [HttpPost("validate")]
    public async Task<IActionResult> Validate(AccessRequest request, CancellationToken ct) =>
        this.ToActionResult(await service.ValidateAsync(request.Token, User.GetUserId()!.Value, ct));

    [HttpPost("entry")]
    [Idempotent(required: false)]
    public async Task<IActionResult> Entry(AccessRequest request, CancellationToken ct) =>
        this.ToActionResult(await service.EntryAsync(request, User.GetUserId()!.Value, ct));

    [HttpPost("exit")]
    [Idempotent(required: false)]
    public async Task<IActionResult> Exit(ExitAccessRequest request, CancellationToken ct) =>
        this.ToActionResult(await service.ExitAsync(request, User.GetUserId()!.Value, ct));

    [HttpGet("current")]
    public async Task<IActionResult> Current(CancellationToken ct) => Ok(await service.CurrentAsync(ct));
}
