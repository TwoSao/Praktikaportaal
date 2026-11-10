using Microsoft.EntityFrameworkCore;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Core.Interfaces.Repositories;

namespace Praktikaportaal.Infrastructure.Persistence.Repositories;

public class ApplicationRepository : IApplicationRepository
{
    private readonly PraktikaportaalDbContext _context;

    public ApplicationRepository(PraktikaportaalDbContext context)
    {
        _context = context;
    }

    // Applications stay visible even if their internship was soft-deleted (application history),
    // so the global filters are ignored here and only the application's own flag is checked.
    private IQueryable<Application> WithDetails() => _context.Applications
        .IgnoreQueryFilters()
        .Where(a => !a.IsDeleted)
        .Include(a => a.Internship)
            .ThenInclude(i => i.CompanyProfile)
        .Include(a => a.StudentProfile)
            .ThenInclude(sp => sp.User);

    public Task<Application?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        WithDetails().FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Application>> GetByStudentAsync(Guid studentProfileId, CancellationToken ct = default) =>
        await WithDetails()
            .Where(a => a.StudentProfileId == studentProfileId)
            .OrderByDescending(a => a.AppliedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Application>> GetByInternshipAsync(Guid internshipId, CancellationToken ct = default) =>
        await WithDetails()
            .Where(a => a.InternshipId == internshipId)
            .OrderByDescending(a => a.AppliedAt)
            .ToListAsync(ct);

    public Task<bool> HasActiveApplicationAsync(Guid studentProfileId, Guid internshipId, CancellationToken ct = default) =>
        _context.Applications.AnyAsync(a =>
            a.StudentProfileId == studentProfileId &&
            a.InternshipId == internshipId &&
            a.Status != ApplicationStatus.Withdrawn, ct);

    public async Task AddAsync(Application application, CancellationToken ct = default) =>
        await _context.Applications.AddAsync(application, ct);
}
