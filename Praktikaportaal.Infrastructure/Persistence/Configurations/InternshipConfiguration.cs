using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Praktikaportaal.Core.Entities;

namespace Praktikaportaal.Infrastructure.Persistence.Configurations;

public class InternshipConfiguration : IEntityTypeConfiguration<Internship>
{
    public void Configure(EntityTypeBuilder<Internship> builder)
    {
        builder.ToTable("Internships");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.Description)
            .IsRequired()
            .HasMaxLength(5000);

        builder.Property(i => i.Requirements)
            .HasMaxLength(3000);

        builder.Property(i => i.Location)
            .HasMaxLength(150);

        builder.Property(i => i.Field)
            .IsRequired()
            .HasMaxLength(100);

        // Search/filter indexes (field + location filters, createdAfter for the Worker).
        builder.HasIndex(i => i.Field);
        builder.HasIndex(i => i.Location);
        builder.HasIndex(i => i.CreatedAt);

        builder.Property(i => i.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(i => i.IsRemote)
            .IsRequired()
            .HasDefaultValue(false);

        // FK: Internship → CompanyProfile.
        // Restrict: deleting a company does not cascade-delete published internships
        // (and their application history).
        builder.HasOne(i => i.CompanyProfile)
            .WithMany(cp => cp.Internships)
            .HasForeignKey(i => i.CompanyProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        // Soft-delete global query filter.
        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}
