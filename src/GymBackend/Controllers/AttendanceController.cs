using GymBackend.Application.Attendance;
using GymBackend.Domain;
using GymBackend.DTOs;
using GymBackend.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymBackend.Controllers;

[ApiController]
[Route(ApiRoutes.V1)]
[Authorize(Roles = Roles.Admin + "," + Roles.Recepcionista)]
public class AttendanceController(IAttendanceService service) : ControllerBase
{
    [HttpPost("entry")]
    [ProducesResponseType<AttendanceMessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RegisterEntry(AttendanceActionRequest request, CancellationToken ct) =>
        this.ToActionResult(await service.RegisterEntryAsync(request.Token, User.GetRut() ?? string.Empty, ct));

    [HttpPost("exit")]
    [ProducesResponseType<AttendanceMessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RegisterExit(AttendanceActionRequest request, CancellationToken ct) =>
        this.ToActionResult(await service.RegisterExitAsync(request.Token, User.GetRut() ?? string.Empty, ct));
}
