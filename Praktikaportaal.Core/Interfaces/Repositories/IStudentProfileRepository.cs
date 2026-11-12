using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;

namespace Praktikaportaal.Core.Interfaces.Repositories;

/// <summary>Data access for <see cref="StudentProfile"/>.</summary>
public interface IStudentProfileRepository
{
    /// <summary>Profile with its user.</summary>
    Task<StudentProfile?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Profile with user, skills (and skill names) and CVs. Tracked for updates.</summary>
    Task<StudentProfile?> GetWithSkillsAsync(Guid id, CancellationToken ct = default);

    /// <summary>All students ordered by name, one page + total count (read-only).</summary>
    Task<(IReadOnlyList<StudentProfile> Items, int TotalCount)> GetPagedAsync(PagingParameters paging, CancellationToken ct = default);
}
