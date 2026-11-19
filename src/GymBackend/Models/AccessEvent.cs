namespace GymBackend.Models;

public sealed class AccessEvent
{
    public long Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public int? UserId { get; set; }
    public int OperatorId { get; set; }
}
