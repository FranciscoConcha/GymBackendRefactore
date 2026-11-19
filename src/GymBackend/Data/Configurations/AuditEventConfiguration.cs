using GymBackend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace GymBackend.Data.Configurations;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.Property(x => x.Action).HasMaxLength(80).IsRequired();
        builder.Property(x => x.TargetType).HasMaxLength(80).IsRequired();
        builder.Property(x => x.TargetId).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Details).HasColumnType("jsonb").IsRequired();
    }
}
