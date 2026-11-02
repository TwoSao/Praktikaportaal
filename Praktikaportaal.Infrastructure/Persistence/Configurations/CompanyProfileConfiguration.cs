using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Praktikaportaal.Core.Entities;

namespace Praktikaportaal.Infrastructure.Persistence.Configurations;

public class CompanyProfileConfiguration : IEntityTypeConfiguration<CompanyProfile>
{
    public void Configure(EntityTypeBuilder<CompanyProfile> builder)
    {
        builder.ToTable("CompanyProfiles");
        builder.HasKey(cp => cp.Id);

        builder.Property(cp => cp.CompanyName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(cp => cp.RegistrationCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(cp => cp.Description)
            .HasMaxLength(3000);

        builder.Property(cp => cp.Website)
            .HasMaxLength(300);

        builder.Property(cp => cp.Address)
            .HasMaxLength(300);

        builder.Property(cp => cp.ContactEmail)
            .HasMaxLength(256);

        builder.Property(cp => cp.ContactPhone)
            .HasMaxLength(30);

        // 1-to-1: CompanyProfile ← User.
        builder.HasOne(cp => cp.User)
            .WithOne(u => u.CompanyProfile)
            .HasForeignKey<CompanyProfile>(cp => cp.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Soft-delete global query filter.
        builder.HasQueryFilter(cp => !cp.IsDeleted);
    }
}
