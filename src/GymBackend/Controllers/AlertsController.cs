using GymBackend.Data;
using GymBackend.Domain;
using GymBackend.Extensions;
using GymBackend.Security;
using GymBackend.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymBackend.Controllers;

[ApiController]
[RequestValidation]
[Authorize]
public sealed class AlertsController(AppDbContext db, IGymClock clock) : ControllerBase
{
    [HttpGet("api/v1/me/alerts")]
    public Task<IActionResult> Mine(CancellationToken ct) => List(User.GetUserId()!.Value, null, null, ct);

    [HttpGet("api/v1/admin/alerts")]
    [Authorize(Roles = Roles.Admin)]
    public Task<IActionResult> All(DateOnly? from, DateOnly? to, CancellationToken ct) => List(null, from, to, ct);

    private async Task<IActionResult> List(int? userId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        if (from > to) return BadRequest(new { message = "El período no es válido." });
        return Ok(await db.AttendanceAlerts.AsNoTracking()
            .Where(a => (!userId.HasValue || a.UserId == userId)
                && (!from.HasValue || a.Reservation.Date >= from) && (!to.HasValue || a.Reservation.Date <= to))
            .OrderBy(a => a.AcknowledgedAt != null).ThenByDescending(a => a.CreatedAt).ThenBy(a => a.Id)
            .Select(a => new
            {
                a.Id,
                a.ReservationId,
                a.Reservation.Date,
                startTime = a.Reservation.ScheduleSlot.StartTime.ToString("HH:mm"),
                endTime = a.Reservation.ScheduleSlot.EndTime.ToString("HH:mm"),
                a.CreatedAt,
                a.AcknowledgedAt
            })
            .ToArrayAsync(ct));
    }

    [HttpPost("api/v1/me/alerts/{id:int}/acknowledge")]
    public async Task<IActionResult> Acknowledge(int id, CancellationToken ct)
    {
        var userId = User.GetUserId()!.Value;
        var count = await db.AttendanceAlerts.Where(a => a.Id == id && a.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.AcknowledgedAt, a => a.AcknowledgedAt ?? clock.UtcNow), ct);
        return count == 0 ? NotFound(new { message = "Alerta no encontrada." }) : Ok(new { message = "Alerta leída." });
    }
}
