using GymBackend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace GymBackend.Data.Configurations;

public sealed class SportBranchConfiguration : IEntityTypeConfiguration<SportBranch>
{
    public void Configure(EntityTypeBuilder<SportBranch> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(80).IsRequired();
        builder.Property<string>("NormalizedName").HasComputedColumnSql("lower(\"Name\")", stored: true);
        builder.HasIndex("NormalizedName").IsUnique();
    }
}
