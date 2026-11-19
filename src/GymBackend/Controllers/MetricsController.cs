using GymBackend.Application.Reporting;
using GymBackend.Domain;
using GymBackend.Security;
using GymBackend.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymBackend.Controllers;

[ApiController]
[RequestValidation]
[Authorize(Roles = Roles.Admin)]
public sealed class MetricsController(MetricsService service, IGymClock clock) : ControllerBase
{
    [HttpGet("api/v1/admin/metrics")]
    public async Task<IActionResult> Get(DateOnly? from, DateOnly? to, int? branchId, string? category, CancellationToken ct)
    {
        var filter = new ReportFilter(from, to, branchId, category).Resolve(clock);
        return filter.IsValid ? Ok(await service.GetAsync(filter, ct)) : BadRequest(new { message = "Los filtros no son válidos." });
    }
}
