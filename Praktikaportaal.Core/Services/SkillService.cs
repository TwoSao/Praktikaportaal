using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Interfaces;
using Praktikaportaal.Core.Interfaces.Repositories;

namespace Praktikaportaal.Core.Services;

public class SkillService : ISkillService
{
    private readonly ISkillRepository _skills;
    private readonly IUnitOfWork _unitOfWork;

    public SkillService(ISkillRepository skills, IUnitOfWork unitOfWork)
    {
        _skills = skills;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<SkillDto>> GetAllSkillsAsync(CancellationToken ct = default)
    {
        var skills = await _skills.GetAllAsync(ct);
        return skills.Select(MapToDto).ToList();
    }

    public async Task<SkillDto> CreateSkillAsync(CreateSkillDto dto, CancellationToken ct = default)
    {
        string normalizedName = dto.Name.Trim();

        // Skill names are unique (case-insensitive): return the existing one instead of duplicating.
        var existing = await _skills.GetByNameAsync(normalizedName, ct);
        if (existing != null)
            return MapToDto(existing);

        var skill = new Skill
        {
            Name = normalizedName,
            Description = dto.Description?.Trim()
        };

        await _skills.AddAsync(skill, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(skill);
    }

    private static SkillDto MapToDto(Skill s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Description = s.Description
    };
}
