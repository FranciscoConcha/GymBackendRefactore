namespace GymBackend.Models;

public sealed class AuditEvent
{
    public int Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public int? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string Details { get; set; } = "[]";
}
