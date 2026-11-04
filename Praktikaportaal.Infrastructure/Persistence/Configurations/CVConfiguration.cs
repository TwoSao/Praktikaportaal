using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Praktikaportaal.Core.Entities;

namespace Praktikaportaal.Infrastructure.Persistence.Configurations;

public class CVConfiguration : IEntityTypeConfiguration<CV>
{
    public void Configure(EntityTypeBuilder<CV> builder)
    {
        builder.ToTable("CVs");
        builder.HasKey(cv => cv.Id);

        builder.Property(cv => cv.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(cv => cv.StorageKey)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(cv => cv.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(cv => cv.UploadedAt)
            .IsRequired();

        // FK: CV → StudentProfile.
        builder.HasOne(cv => cv.StudentProfile)
            .WithMany(sp => sp.CVs)
            .HasForeignKey(cv => cv.StudentProfileId)
            .OnDelete(DeleteBehavior.Restrict); // keep CV metadata even if profile is soft-deleted

        // Soft-delete global query filter.
        builder.HasQueryFilter(cv => !cv.IsDeleted);
    }
}
