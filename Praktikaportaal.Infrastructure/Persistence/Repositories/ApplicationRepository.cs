using Microsoft.EntityFrameworkCore;
using Praktikaportaal.Core.DTOs;
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
            .ThenInclude(sp => sp.User)
        .Include(a => a.StudentProfile)
            .ThenInclude(sp => sp.CVs);

    public Task<Application?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        WithDetails().FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<(IReadOnlyList<Application> Items, int TotalCount)> GetByStudentAsync(
        Guid studentProfileId, ApplicationQueryParameters query, CancellationToken ct = default) =>
        PageAsync(WithDetails().Where(a => a.StudentProfileId == studentProfileId), query, ct);

    public Task<(IReadOnlyList<Application> Items, int TotalCount)> GetByInternshipAsync(
        Guid internshipId, ApplicationQueryParameters query, CancellationToken ct = default) =>
        PageAsync(WithDetails().Where(a => a.InternshipId == internshipId), query, ct);

    private static async Task<(IReadOnlyList<Application> Items, int TotalCount)> PageAsync(
        IQueryable<Application> source, ApplicationQueryParameters query, CancellationToken ct)
    {
        if (query.Status.HasValue)
            source = source.Where(a => a.Status == query.Status.Value);

        int total = await source.CountAsync(ct);
        var items = await source
            .OrderByDescending(a => a.AppliedAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<bool> HasActiveApplicationAsync(Guid studentProfileId, Guid internshipId, CancellationToken ct = default) =>
        _context.Applications.AnyAsync(a =>
            a.StudentProfileId == studentProfileId &&
            a.InternshipId == internshipId &&
            a.Status != ApplicationStatus.Withdrawn, ct);

    public async Task AddAsync(Application application, CancellationToken ct = default) =>
        await _context.Applications.AddAsync(application, ct);
}
