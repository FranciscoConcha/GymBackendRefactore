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
        builder.Property(x => x.FirstName).HasMaxLength(80).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(80).IsRequired();
        builder.Ignore(x => x.FullName);
        builder.Property(x => x.AccountStatus).HasConversion<string>().HasMaxLength(20).HasDefaultValue(GymBackend.Domain.AccountStatus.Activa);
        builder.Property(x => x.Role).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(20);
        builder.HasOne(x => x.SportBranch).WithMany().HasForeignKey(x => x.SportBranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.Rut).IsUnique();
        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasIndex(x => x.Username).IsUnique().HasFilter("\"Username\" <> ''");
    }
}
