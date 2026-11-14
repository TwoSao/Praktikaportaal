using Microsoft.EntityFrameworkCore;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Core.Interfaces.Repositories;

namespace Praktikaportaal.Infrastructure.Persistence.Repositories;

public class InternshipRepository : IInternshipRepository
{
    private readonly PraktikaportaalDbContext _context;

    public InternshipRepository(PraktikaportaalDbContext context)
    {
        _context = context;
    }

    private IQueryable<Internship> WithDetails() => _context.Internships
        .Include(i => i.CompanyProfile)
        .Include(i => i.InternshipSkills)
            .ThenInclude(isk => isk.Skill)
        .Include(i => i.Applications);

    public Task<Internship?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        WithDetails().FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<(IReadOnlyList<Internship> Items, int TotalCount)> SearchPublishedAsync(
        InternshipQueryParameters q, CancellationToken ct = default)
    {
        var query = _context.Internships.Where(i => i.Status == InternshipStatus.Published);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            string s = q.Search.Trim().ToLower();
            query = query.Where(i =>
                i.Title.ToLower().Contains(s) ||
                i.Description.ToLower().Contains(s) ||
                i.Field.ToLower().Contains(s) ||
                (i.Location != null && i.Location.ToLower().Contains(s)) ||
                i.CompanyProfile.CompanyName.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(q.Field))
        {
            string field = q.Field.Trim().ToLower();
            query = query.Where(i => i.Field.ToLower() == field);
        }

        if (!string.IsNullOrWhiteSpace(q.Location))
        {
            string location = q.Location.Trim().ToLower();
            query = query.Where(i => i.Location != null && i.Location.ToLower().Contains(location));
        }

        if (q.IsRemote.HasValue)
            query = query.Where(i => i.IsRemote == q.IsRemote.Value);

        if (q.CreatedAfter.HasValue)
            query = query.Where(i => i.CreatedAt > q.CreatedAfter.Value);

        int total = await query.CountAsync(ct);

        query = q.Sort switch
        {
            InternshipSort.Oldest => query.OrderBy(i => i.CreatedAt),
            InternshipSort.Deadline => query
                .OrderBy(i => i.ApplicationDeadline == null)
                .ThenBy(i => i.ApplicationDeadline)
                .ThenByDescending(i => i.CreatedAt),
            InternshipSort.Title => query.OrderBy(i => i.Title),
            _ => query.OrderByDescending(i => i.CreatedAt)
        };

        var items = await query
            .Skip(q.Skip)
            .Take(q.PageSize)
            .Include(i => i.CompanyProfile)
            .Include(i => i.InternshipSkills).ThenInclude(isk => isk.Skill)
            .Include(i => i.Applications)
            .AsSplitQuery()
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<(IReadOnlyList<string> Fields, IReadOnlyList<string> Locations)> GetPublishedFilterValuesAsync(
        CancellationToken ct = default)
    {
        var published = _context.Internships.Where(i => i.Status == InternshipStatus.Published);

        var fields = await published.Select(i => i.Field).Distinct().OrderBy(f => f).ToListAsync(ct);
        var locations = await published
            .Where(i => i.Location != null && i.Location != "")
            .Select(i => i.Location!)
            .Distinct()
            .OrderBy(l => l)
            .ToListAsync(ct);

        return (fields, locations);
    }

    public async Task<IReadOnlyList<Internship>> GetOpenAsync(DateTime? createdAfter, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var query = WithDetails().AsNoTracking()
            .Where(i => i.Status == InternshipStatus.Published &&
                        (i.ApplicationDeadline == null || i.ApplicationDeadline >= now));

        if (createdAfter.HasValue)
            query = query.Where(i => i.CreatedAt > createdAfter.Value);

        return await query.AsSplitQuery().ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Internship>> GetByCompanyAsync(Guid companyProfileId, CancellationToken ct = default) =>
        await WithDetails()
            .Where(i => i.CompanyProfileId == companyProfileId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(Internship internship, CancellationToken ct = default) =>
        await _context.Internships.AddAsync(internship, ct);
}
