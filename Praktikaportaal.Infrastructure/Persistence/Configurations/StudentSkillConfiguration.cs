using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Praktikaportaal.Core.Entities;

namespace Praktikaportaal.Infrastructure.Persistence.Configurations;

public class StudentSkillConfiguration : IEntityTypeConfiguration<StudentSkill>
{
    public void Configure(EntityTypeBuilder<StudentSkill> builder)
    {
        builder.ToTable("StudentSkills");

        // Composite primary key.
        builder.HasKey(ss => new { ss.StudentProfileId, ss.SkillId });

        builder.Property(ss => ss.Level)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        // FK: StudentSkill → StudentProfile.
        builder.HasOne(ss => ss.StudentProfile)
            .WithMany(sp => sp.StudentSkills)
            .HasForeignKey(ss => ss.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade); // removing profile removes its skill tags

        // FK: StudentSkill → Skill.
        builder.HasOne(ss => ss.Skill)
            .WithMany(s => s.StudentSkills)
            .HasForeignKey(ss => ss.SkillId)
            .OnDelete(DeleteBehavior.Restrict); // skills are shared; can't cascade-delete
    }
}
