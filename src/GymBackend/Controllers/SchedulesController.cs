using GymBackend.DTOs;
using GymBackend.Application.Schedules;
using GymBackend.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SchedulesController : ControllerBase
{
    private readonly IScheduleService _service;

    public SchedulesController(IScheduleService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType<ScheduleResponse[]>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSchedules([FromQuery] string? date = null, CancellationToken ct = default)
    {
        return this.ToActionResult(await _service.GetEnabledAsync(date, ct));
    }
}

