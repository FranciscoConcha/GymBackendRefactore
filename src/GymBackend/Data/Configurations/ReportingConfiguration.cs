using GymBackend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymBackend.Data.Configurations;

public sealed class AttendanceAlertConfiguration : IEntityTypeConfiguration<AttendanceAlert>
{
    public void Configure(EntityTypeBuilder<AttendanceAlert> builder)
    {
        builder.HasIndex(a => a.ReservationId).IsUnique();
        builder.HasOne(a => a.Reservation).WithMany().HasForeignKey(a => a.ReservationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class UserImportConfiguration : IEntityTypeConfiguration<UserImport>
{
    public void Configure(EntityTypeBuilder<UserImport> builder)
    {
        builder.Property(i => i.Status).HasMaxLength(20);
        builder.HasOne<User>().WithMany().HasForeignKey(i => i.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}
