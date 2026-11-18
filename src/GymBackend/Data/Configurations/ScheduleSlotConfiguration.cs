using GymBackend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymBackend.Data.Configurations;

public sealed class ScheduleSlotConfiguration : IEntityTypeConfiguration<ScheduleSlot>
{
    public void Configure(EntityTypeBuilder<ScheduleSlot> builder)
    {
        builder.Property(x => x.Day).HasConversion<int>();
        builder.Property(x => x.StartTime).HasColumnType("time without time zone");
        builder.Property(x => x.EndTime).HasColumnType("time without time zone");
        builder.Property(x => x.Zone).HasMaxLength(64);
        builder.HasIndex(x => new { x.Day, x.StartTime, x.EndTime, x.Zone }).IsUnique();
        builder.ToTable("ScheduleSlots", table =>
        {
            table.HasCheckConstraint("CK_ScheduleSlots_Capacity", "\"Capacity\" > 0");
            table.HasCheckConstraint("CK_ScheduleSlots_TimeRange", "\"EndTime\" > \"StartTime\"");
        });
    }
}
