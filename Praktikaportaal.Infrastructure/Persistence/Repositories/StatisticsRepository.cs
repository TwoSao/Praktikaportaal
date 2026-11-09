using Microsoft.EntityFrameworkCore;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Core.Interfaces.Repositories;

namespace Praktikaportaal.Infrastructure.Persistence.Repositories;

public class StatisticsRepository : IStatisticsRepository
{
    private readonly PraktikaportaalDbContext _context;

    public StatisticsRepository(PraktikaportaalDbContext context)
    {
        _context = context;
    }

    // Statistics are history: keep applications whose internship was soft-deleted.
    private IQueryable<Application> Applications(DateTime? from, DateTime? toExclusive)
    {
        var query = _context.Applications.IgnoreQueryFilters().AsNoTracking().Where(a => !a.IsDeleted);

        if (from.HasValue)
            query = query.Where(a => a.AppliedAt >= from.Value);
        if (toExclusive.HasValue)
            query = query.Where(a => a.AppliedAt < toExclusive.Value);

        return query;
    }

    public async Task<IReadOnlyList<StatusCountDto>> CountByStatusAsync(DateTime? from, DateTime? toExclusive, CancellationToken ct = default) =>
        await Applications(from, toExclusive)
            .GroupBy(a => a.Status)
            .Select(g => new StatusCountDto { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

    public async Task<IReadOnlyList<InternshipCountDto>> GetTopInternshipsAsync(
        DateTime? from, DateTime? toExclusive, int take, CancellationToken ct = default) =>
        await Applications(from, toExclusive)
            .GroupBy(a => new { a.InternshipId, a.Internship.Title, a.Internship.CompanyProfile.CompanyName })
            .Select(g => new InternshipCountDto
            {
                InternshipId = g.Key.InternshipId,
                Title = g.Key.Title,
                CompanyName = g.Key.CompanyName,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Title)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<FieldCountDto>> CountByFieldAsync(DateTime? from, DateTime? toExclusive, CancellationToken ct = default) =>
        await Applications(from, toExclusive)
            .GroupBy(a => a.Internship.Field)
            .Select(g => new FieldCountDto { Field = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Field)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DateTime>> GetApplicationDatesAsync(DateTime? from, DateTime? toExclusive, CancellationToken ct = default) =>
        await Applications(from, toExclusive)
            .Select(a => a.AppliedAt)
            .ToListAsync(ct);

    public async Task<PortalOverviewDto> GetOverviewAsync(CancellationToken ct = default) => new()
    {
        Students = await _context.StudentProfiles.CountAsync(ct),
        Companies = await _context.CompanyProfiles.CountAsync(ct),
        PublishedInternships = await _context.Internships.CountAsync(i => i.Status == InternshipStatus.Published, ct)
    };
}
