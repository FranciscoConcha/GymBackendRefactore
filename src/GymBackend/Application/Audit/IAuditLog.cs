namespace GymBackend.Application.Audit;

public interface IAuditLog
{
    void Add(string action, object target, params string[] changedFields);
}
