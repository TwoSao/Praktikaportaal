using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Praktikaportaal.Core.Entities;

namespace Praktikaportaal.Infrastructure.Persistence.Configurations;

public class SavedInternshipConfiguration : IEntityTypeConfiguration<SavedInternship>
{
    public void Configure(EntityTypeBuilder<SavedInternship> builder)
    {
        builder.ToTable("SavedInternships");

        // Composite primary key — a student can save any given internship only once.
        builder.HasKey(si => new { si.StudentProfileId, si.InternshipId });

        builder.Property(si => si.SavedAt)
            .IsRequired();

        // FK: SavedInternship → StudentProfile.
        builder.HasOne(si => si.StudentProfile)
            .WithMany(sp => sp.SavedInternships)
            .HasForeignKey(si => si.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade); // removing profile removes bookmarks

        // FK: SavedInternship → Internship.
        builder.HasOne(si => si.Internship)
            .WithMany(i => i.SavedByStudents)
            .HasForeignKey(si => si.InternshipId)
            .OnDelete(DeleteBehavior.Cascade); // removing internship removes bookmarks
    }
}
