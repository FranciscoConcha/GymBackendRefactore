using System.Globalization;
using System.Text.Json;
using GymBackend.Data;
using GymBackend.Extensions;
using GymBackend.Models;
using GymBackend.Time;
namespace GymBackend.Application.Audit;

public sealed class AuditLog(AppDbContext context, IGymClock clock, IHttpContextAccessor accessor) : IAuditLog
{
    public void Add(string action, object target, params string[] changedFields)
    {
        var audit = new AuditEvent
        {
            OccurredAt = clock.UtcNow,
            ActorUserId = accessor.HttpContext?.User.GetUserId(),
            Action = action,
            TargetType = target is User ? "User" : "SportBranch",
            Details = JsonSerializer.Serialize(changedFields),
        };
        // Resolve generated identifiers inside the same transaction as the business changes.
        context.EnqueueAudit(() =>
        {
            audit.TargetId = (target switch
            {
                User user => user.Id,
                SportBranch branch => branch.Id,
                _ => throw new InvalidOperationException("Tipo de auditor\u00eda no admitido."),
            }).ToString(CultureInfo.InvariantCulture);
            return audit;
        });
    }
}
