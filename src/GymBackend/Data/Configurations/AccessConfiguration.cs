using GymBackend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymBackend.Data.Configurations;

public sealed class VisitConfiguration : IEntityTypeConfiguration<Visit>
{
    public void Configure(EntityTypeBuilder<Visit> builder)
    {
        builder.HasIndex(v => v.UserId).IsUnique().HasFilter("\"ExitAt\" IS NULL");
        builder.HasIndex(v => new { v.GroupBookingId, v.UserId });
        builder.HasIndex(v => v.Date);
        builder.Property(v => v.DocumentType).HasMaxLength(32);
        builder.HasOne(v => v.User).WithMany().HasForeignKey(v => v.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(v => v.EntryOperator).WithMany().HasForeignKey(v => v.EntryOperatorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(v => v.ExitOperator).WithMany().HasForeignKey(v => v.ExitOperatorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(v => v.Reservation).WithMany().HasForeignKey(v => v.ReservationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(v => v.GroupBooking).WithMany().HasForeignKey(v => v.GroupBookingId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Visit_Session", "(\"ReservationId\" IS NULL) <> (\"GroupBookingId\" IS NULL)");
            t.HasCheckConstraint("CK_Visit_Document", "\"DocumentType\" IN ('CarnetIdentidad', 'TNE', 'CredencialUniversitaria')");
            t.HasCheckConstraint("CK_Visit_Exit", "\"ExitAt\" IS NULL OR \"ExitAt\" >= \"EntryAt\"");
        });
    }
}

public sealed class AccessEventConfiguration : IEntityTypeConfiguration<AccessEvent>
{
    public void Configure(EntityTypeBuilder<AccessEvent> builder)
    {
        builder.Property(e => e.Action).HasMaxLength(16);
        builder.Property(e => e.Result).HasMaxLength(16);
        builder.Property(e => e.Reason).HasMaxLength(512);
        builder.HasIndex(e => e.OccurredAt);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.OperatorId).OnDelete(DeleteBehavior.Restrict);
    }
}
