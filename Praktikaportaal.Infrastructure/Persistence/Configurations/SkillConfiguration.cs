using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Praktikaportaal.Core.Entities;

namespace Praktikaportaal.Infrastructure.Persistence.Configurations;

public class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("Skills");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.Description)
            .HasMaxLength(500);

        // Skill names must be globally unique.
        builder.HasIndex(s => s.Name)
            .IsUnique()
            .HasDatabaseName("IX_Skills_Name");

        // Soft-delete global query filter.
        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
