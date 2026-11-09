using Microsoft.EntityFrameworkCore;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Interfaces.Repositories;

namespace Praktikaportaal.Infrastructure.Persistence.Repositories;

public class SkillRepository : ISkillRepository
{
    private readonly PraktikaportaalDbContext _context;

    public SkillRepository(PraktikaportaalDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Skill>> GetAllAsync(CancellationToken ct = default) =>
        await _context.Skills.OrderBy(s => s.Name).ToListAsync(ct);

    public Task<Skill?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        string lower = name.Trim().ToLower();
        return _context.Skills.FirstOrDefaultAsync(s => s.Name.ToLower() == lower, ct);
    }

    public async Task AddAsync(Skill skill, CancellationToken ct = default) =>
        await _context.Skills.AddAsync(skill, ct);
}
