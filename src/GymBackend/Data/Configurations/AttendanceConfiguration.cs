using GymBackend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymBackend.Data.Configurations;

public sealed class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> builder)
    {
        builder.Property(x => x.Date).HasColumnType("date");
        builder.Property(x => x.EntryTime).HasColumnType("timestamp with time zone");
        builder.Property(x => x.ExitTime).HasColumnType("timestamp with time zone");
        builder.Property(x => x.EntryAdminRut).HasMaxLength(9);
        builder.Property(x => x.ExitAdminRut).HasMaxLength(9);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Reservation).WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => new { x.UserId, x.Date }).IsUnique();
    }
}
