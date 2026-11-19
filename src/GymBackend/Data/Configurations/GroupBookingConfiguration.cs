using GymBackend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymBackend.Data.Configurations;

public sealed class GroupBookingConfiguration : IEntityTypeConfiguration<GroupBooking>
{
    public void Configure(EntityTypeBuilder<GroupBooking> builder)
    {
        builder.Property(g => g.Mode).HasMaxLength(32);
        builder.Property(g => g.Status).HasMaxLength(16);
        builder.Property(g => g.IdempotencyKey).HasMaxLength(200);
        builder.HasOne(g => g.SportBranch).WithMany().HasForeignKey(g => g.SportBranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(g => g.ScheduleSlot).WithMany().HasForeignKey(g => g.ScheduleSlotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(g => g.CreatedBy).WithMany().HasForeignKey(g => g.CreatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(g => new { g.ScheduleSlotId, g.Date, g.Status });
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Group_Seats", "\"Seats\" > 0");
            t.HasCheckConstraint("CK_Group_Mode", "\"Mode\" IN ('Cupos', 'TotalDisponible')");
            t.HasCheckConstraint("CK_Group_Status", "\"Status\" IN ('Activa', 'Cancelada')");
        });
    }
}
