using Microsoft.EntityFrameworkCore;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Interfaces;
using Praktikaportaal.Infrastructure.Persistence;

namespace Praktikaportaal.Infrastructure.Services;

public class SkillService : ISkillService
{
    private readonly PraktikaportaalDbContext _context;

    public SkillService(PraktikaportaalDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SkillDto>> GetAllSkillsAsync(CancellationToken ct = default)
    {
        return await _context.Skills
            .OrderBy(s => s.Name)
            .Select(s => new SkillDto
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description
            })
            .ToListAsync(ct);
    }

    public async Task<SkillDto> CreateSkillAsync(CreateSkillDto dto, CancellationToken ct = default)
    {
        string normalizedName = dto.Name.Trim();

        var existing = await _context.Skills
            .FirstOrDefaultAsync(s => s.Name.ToLower() == normalizedName.ToLower(), ct);

        if (existing != null)
        {
            return new SkillDto
            {
                Id = existing.Id,
                Name = existing.Name,
                Description = existing.Description
            };
        }

        var skill = new Skill
        {
            Name = normalizedName,
            Description = dto.Description?.Trim()
        };

        await _context.Skills.AddAsync(skill, ct);
        await _context.SaveChangesAsync(ct);

        return new SkillDto
        {
            Id = skill.Id,
            Name = skill.Name,
            Description = skill.Description
        };
    }
}
