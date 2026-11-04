using Praktikaportaal.Core.DTOs;

namespace Praktikaportaal.Core.Interfaces;

public interface ISkillService
{
    Task<IReadOnlyList<SkillDto>> GetAllSkillsAsync(CancellationToken ct = default);
    Task<SkillDto> CreateSkillAsync(CreateSkillDto dto, CancellationToken ct = default);
}
