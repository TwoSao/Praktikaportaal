using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Infrastructure.Persistence.Configurations;

public class ApplicationConfiguration : IEntityTypeConfiguration<Application>
{
    public void Configure(EntityTypeBuilder<Application> builder)
    {
        builder.ToTable("Applications");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.CoverLetter)
            .HasMaxLength(5000);

        builder.Property(a => a.Status)
            .IsRequired();

        builder.Property(a => a.AppliedAt)
            .IsRequired();

        // FK: Application → StudentProfile.
        // Restrict: student deletion does not cascade-delete application history.
        builder.HasOne(a => a.StudentProfile)
            .WithMany(sp => sp.Applications)
            .HasForeignKey(a => a.StudentProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK: Application → Internship.
        // Restrict: internship deletion does not cascade-delete application history.
        builder.HasOne(a => a.Internship)
            .WithMany(i => i.Applications)
            .HasForeignKey(a => a.InternshipId)
            .OnDelete(DeleteBehavior.Restrict);

        // Filtered unique index: a student may not have more than one
        // non-Withdrawn application for the same internship at a time.
        // ApplicationStatus.Withdrawn == 3 — excluded from the uniqueness constraint,
        // so a student may reapply after withdrawing.
        builder.HasIndex(a => new { a.StudentProfileId, a.InternshipId })
            .IsUnique()
            .HasFilter($"[Status] <> {(int)ApplicationStatus.Withdrawn}")
            .HasDatabaseName("IX_Applications_Student_Internship_Active");

        // Soft-delete global query filter.
        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}
