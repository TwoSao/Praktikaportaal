using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Praktikaportaal.Core.Entities;

namespace Praktikaportaal.Infrastructure.Persistence.Configurations;

public class StudentProfileConfiguration : IEntityTypeConfiguration<StudentProfile>
{
    public void Configure(EntityTypeBuilder<StudentProfile> builder)
    {
        builder.ToTable("StudentProfiles");
        builder.HasKey(sp => sp.Id);

        builder.Property(sp => sp.SchoolName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(sp => sp.StudyProgram)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(sp => sp.Course)
            .IsRequired();

        builder.Property(sp => sp.Description)
            .HasMaxLength(2000);

        builder.Property(sp => sp.PhoneNumber)
            .HasMaxLength(30);

        builder.Property(sp => sp.Location)
            .HasMaxLength(150);

        // 1-to-1: StudentProfile ← User.
        // HasOne/WithOne keeps the FK on StudentProfile (dependent side).
        builder.HasOne(sp => sp.User)
            .WithOne(u => u.StudentProfile)
            .HasForeignKey<StudentProfile>(sp => sp.UserId)
            .OnDelete(DeleteBehavior.Restrict); // preserve profile history

        // Soft-delete global query filter.
        builder.HasQueryFilter(sp => !sp.IsDeleted);
    }
}
