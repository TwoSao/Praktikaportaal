using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Praktikaportaal.Core.Entities;

namespace Praktikaportaal.Infrastructure.Persistence.Configurations;

public class InternshipSkillConfiguration : IEntityTypeConfiguration<InternshipSkill>
{
    public void Configure(EntityTypeBuilder<InternshipSkill> builder)
    {
        builder.ToTable("InternshipSkills");

        // Composite primary key.
        builder.HasKey(isk => new { isk.InternshipId, isk.SkillId });

        builder.Property(isk => isk.RequiredLevel)
            .HasConversion<string>()
            .HasMaxLength(20);

        // FK: InternshipSkill → Internship.
        builder.HasOne(isk => isk.Internship)
            .WithMany(i => i.InternshipSkills)
            .HasForeignKey(isk => isk.InternshipId)
            .OnDelete(DeleteBehavior.Cascade); // removing internship removes its skill tags

        // FK: InternshipSkill → Skill.
        builder.HasOne(isk => isk.Skill)
            .WithMany(s => s.InternshipSkills)
            .HasForeignKey(isk => isk.SkillId)
            .OnDelete(DeleteBehavior.Restrict);

        // Hide join rows whose principal is soft-deleted (matches the principals' query filters).
        builder.HasQueryFilter(x => !x.Internship.IsDeleted && !x.Skill.IsDeleted);
    }
}
