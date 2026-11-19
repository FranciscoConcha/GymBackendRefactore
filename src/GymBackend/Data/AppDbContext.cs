using GymBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace GymBackend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    public DbSet<SportBranch> SportBranches => Set<SportBranch>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    private readonly List<Func<AuditEvent>> _pendingAudits = [];
    internal void EnqueueAudit(Func<AuditEvent> audit) => _pendingAudits.Add(audit);

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_pendingAudits.Count == 0) return await base.SaveChangesAsync(cancellationToken);
        await using var transaction = Database.CurrentTransaction is null
            ? await Database.BeginTransactionAsync(cancellationToken) : null;
        var count = await base.SaveChangesAsync(cancellationToken);
        AuditEvents.AddRange(_pendingAudits.Select(create => create()));
        count += await base.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        _pendingAudits.Clear();
        return count;
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    public DbSet<ScheduleSlot> ScheduleSlots => Set<ScheduleSlot>();

    public DbSet<Attendance> Attendances => Set<Attendance>();
}

