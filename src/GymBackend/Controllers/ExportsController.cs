using GymBackend.Application.Excel;
using GymBackend.Application.Reporting;
using GymBackend.Domain;
using GymBackend.Extensions;
using GymBackend.Security;
using GymBackend.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymBackend.Controllers;

[ApiController]
[RequestValidation]
[Authorize(Roles = Roles.Admin)]
public sealed class ExportsController(ExportService service, IGymClock clock) : ControllerBase
{
    [HttpPost("api/v1/admin/exports")]
    public async Task<IActionResult> Create(ReportFilter request, CancellationToken ct)
    {
        var filter = request.Resolve(clock);
        if (!filter.IsValid) return BadRequest(new { message = "Los filtros no son válidos." });
        try
        {
            var stream = await service.CreateAsync(filter, User.GetUserId()!.Value, ct);
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "gym.xlsx");
        }
        catch (InvalidDataException exception) { return BadRequest(new { message = exception.Message }); }
    }
}
