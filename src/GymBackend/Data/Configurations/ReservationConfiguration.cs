using GymBackend.Domain;
using GymBackend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymBackend.Data.Configurations;

public sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.Property(x => x.CategoryAtBooking).HasMaxLength(20);
        builder.HasOne<SportBranch>().WithMany().HasForeignKey(x => x.SportBranchIdAtBooking).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Date).HasColumnType("date");
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        builder.Property(x => x.Status).HasMaxLength(16).HasConversion(
            status => ReservationStatuses.Name(status),
            value => ReservationStatuses.Parse(value));
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ScheduleSlot).WithMany().HasForeignKey(x => x.ScheduleSlotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.UserId, x.ScheduleSlotId, x.Date }).IsUnique().HasFilter("\"Status\" = 'Reservado'");
        builder.HasIndex(x => new { x.ScheduleSlotId, x.Date, x.Status });
    }
}
