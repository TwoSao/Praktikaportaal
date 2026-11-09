using Praktikaportaal.Core.Entities;

namespace Praktikaportaal.Core.Interfaces.Repositories;

/// <summary>Data access for <see cref="Skill"/>.</summary>
public interface ISkillRepository
{
    Task<IReadOnlyList<Skill>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Case-insensitive lookup by name.</summary>
    Task<Skill?> GetByNameAsync(string name, CancellationToken ct = default);

    Task<IReadOnlyList<Skill>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);

    Task AddAsync(Skill skill, CancellationToken ct = default);
}
