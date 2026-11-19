using GymBackend.Application.Groups;
using GymBackend.Domain;
using GymBackend.DTOs;
using GymBackend.Extensions;
using GymBackend.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymBackend.Controllers;

[ApiController]
[RequestValidation]
[Authorize]
public sealed class GroupBookingsController(GroupBookingService service) : ControllerBase
{
    [HttpPost("api/v1/group-bookings")]
    [Authorize(Roles = Roles.Admin)]
    [Idempotent(required: true)]
    public async Task<IActionResult> Create(CreateGroupBookingRequest request, CancellationToken ct) =>
        this.ToActionResult(await service.CreateAsync(request, User.GetUserId()!.Value, Request.Headers["Idempotency-Key"].ToString(), ct));

    [HttpGet("api/v1/group-bookings")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> All(DateOnly? from, DateOnly? to, int? branchId, CancellationToken ct) =>
        Ok(await service.ListAsync(from, to, branchId, null, ct));

    [HttpPost("api/v1/group-bookings/{id:int}/cancel")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct) => this.ToActionResult(await service.CancelAsync(id, ct));

    [HttpGet("api/v1/me/group-bookings")]
    public async Task<IActionResult> Mine(CancellationToken ct) => Ok(await service.ListAsync(null, null, null, User.GetUserId()!.Value, ct));
}
