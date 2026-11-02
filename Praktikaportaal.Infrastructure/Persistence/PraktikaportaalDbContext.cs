using Microsoft.EntityFrameworkCore;
using Praktikaportaal.Core.Entities;

namespace Praktikaportaal.Infrastructure.Persistence;

/// <summary>
/// Main EF Core database context for Praktikaportaal.
/// Configurations are split into separate IEntityTypeConfiguration classes
/// to keep this class focused on registration and cross-cutting concerns.
/// </summary>
public class PraktikaportaalDbContext : DbContext
{
    public PraktikaportaalDbContext(DbContextOptions<PraktikaportaalDbContext> options)
        : base(options)
    {
    }

    // ── DbSets ────────────────────────────────────────────────────────────

    public DbSet<User> Users => Set<User>();
    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
    public DbSet<CompanyProfile> CompanyProfiles => Set<CompanyProfile>();
    public DbSet<Internship> Internships => Set<Internship>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<StudentSkill> StudentSkills => Set<StudentSkill>();
    public DbSet<InternshipSkill> InternshipSkills => Set<InternshipSkill>();
    public DbSet<CV> CVs => Set<CV>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<SavedInternship> SavedInternships => Set<SavedInternship>();

    // ── Model configuration ───────────────────────────────────────────────

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration<T> classes found in this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PraktikaportaalDbContext).Assembly);
    }

    // ── Audit timestamps ──────────────────────────────────────────────────

    /// <summary>
    /// Automatically populates CreatedAt and UpdatedAt (UTC) on BaseEntity
    /// instances before every save operation.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetAuditTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        SetAuditTimestamps();
        return base.SaveChanges();
    }

    private void SetAuditTimestamps()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = null; // explicitly null on creation
                    break;

                case EntityState.Modified:
                    // Prevent accidental overwrite of original CreatedAt.
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }
}
