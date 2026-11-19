using GymBackend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymBackend.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(x => x.Rut).HasMaxLength(10);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.Username).HasMaxLength(64);
        builder.Property(x => x.Name).HasMaxLength(256);
        builder.HasIndex(x => x.Rut).IsUnique();
        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasIndex(x => x.Username).IsUnique().HasFilter("\"Username\" <> ''");
    }
}
