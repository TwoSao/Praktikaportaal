using Microsoft.EntityFrameworkCore;
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

    public async Task<IReadOnlyList<Internship>> GetPublishedAsync(
        string? search, bool? isRemote, CancellationToken ct = default)
    {
        var query = WithDetails().Where(i => i.Status == InternshipStatus.Published);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string s = search.Trim().ToLower();
            query = query.Where(i =>
                i.Title.ToLower().Contains(s) ||
                i.Description.ToLower().Contains(s) ||
                i.Field.ToLower().Contains(s) ||
                (i.Location != null && i.Location.ToLower().Contains(s)) ||
                i.CompanyProfile.CompanyName.ToLower().Contains(s));
        }

        if (isRemote.HasValue)
            query = query.Where(i => i.IsRemote == isRemote.Value);

        return await query.OrderByDescending(i => i.CreatedAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Internship>> GetByCompanyAsync(Guid companyProfileId, CancellationToken ct = default) =>
        await WithDetails()
            .Where(i => i.CompanyProfileId == companyProfileId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

    public Task<bool> IsOwnedByCompanyAsync(Guid internshipId, Guid companyProfileId, CancellationToken ct = default) =>
        _context.Internships.AnyAsync(i => i.Id == internshipId && i.CompanyProfileId == companyProfileId, ct);

    public async Task AddAsync(Internship internship, CancellationToken ct = default) =>
        await _context.Internships.AddAsync(internship, ct);
}
