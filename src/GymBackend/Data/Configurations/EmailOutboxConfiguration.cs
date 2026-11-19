using GymBackend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymBackend.Data.Configurations;

public sealed class EmailOutboxConfiguration : IEntityTypeConfiguration<EmailOutbox>
{
    public void Configure(EntityTypeBuilder<EmailOutbox> builder)
    {
        builder.Property(x => x.To).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Body).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(256);
        builder.HasIndex(x => new { x.SentAt, x.NextAttemptAt });
    }
}
