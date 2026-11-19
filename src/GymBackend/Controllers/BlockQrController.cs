using GymBackend.Application.Qr;
using GymBackend.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymBackend.Controllers;

[ApiController]
[Route("api/v1/me")]
[Authorize]
public sealed class BlockQrController(BlockQrService service) : ControllerBase
{
    [HttpGet("reservations/{id:int}/qr")]
    public async Task<IActionResult> Reservation(int id, CancellationToken ct) =>
        this.ToActionResult(await service.IssueAsync(User.GetUserId()!.Value, id, false, ct));

    [HttpGet("group-bookings/{id:int}/qr")]
    public async Task<IActionResult> Group(int id, CancellationToken ct) =>
        this.ToActionResult(await service.IssueAsync(User.GetUserId()!.Value, id, true, ct));
}
