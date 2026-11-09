using Microsoft.EntityFrameworkCore;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Interfaces.Repositories;

namespace Praktikaportaal.Infrastructure.Persistence.Repositories;

public class StudentProfileRepository : IStudentProfileRepository
{
    private readonly PraktikaportaalDbContext _context;

    public StudentProfileRepository(PraktikaportaalDbContext context)
    {
        _context = context;
    }

    public Task<StudentProfile?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.StudentProfiles
            .Include(sp => sp.User)
            .FirstOrDefaultAsync(sp => sp.Id == id, ct);

    public Task<StudentProfile?> GetWithSkillsAsync(Guid id, CancellationToken ct = default) =>
        WithDetails(_context.StudentProfiles).FirstOrDefaultAsync(sp => sp.Id == id, ct);

    public async Task<(IReadOnlyList<StudentProfile> Items, int TotalCount)> GetPagedAsync(
        PagingParameters paging, CancellationToken ct = default)
    {
        int total = await _context.StudentProfiles.CountAsync(ct);

        var items = await WithDetails(_context.StudentProfiles.AsNoTracking())
            .OrderBy(sp => sp.User.LastName)
            .ThenBy(sp => sp.User.FirstName)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .AsSplitQuery()
            .ToListAsync(ct);

        return (items, total);
    }

    private static IQueryable<StudentProfile> WithDetails(IQueryable<StudentProfile> source) => source
        .Include(sp => sp.User)
        .Include(sp => sp.StudentSkills).ThenInclude(ss => ss.Skill)
        .Include(sp => sp.CVs);
}
