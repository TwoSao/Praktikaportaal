using Praktikaportaal.Core.Entities;

namespace Praktikaportaal.Core.Interfaces.Repositories;

/// <summary>Data access for <see cref="CV"/> records (metadata only; files live in <see cref="IFileStorage"/>).</summary>
public interface ICvRepository
{
    /// <summary>All active (not deleted) CVs of the student, newest first. Tracked for updates.</summary>
    Task<IReadOnlyList<CV>> GetByStudentAsync(Guid studentProfileId, CancellationToken ct = default);

    Task AddAsync(CV cv, CancellationToken ct = default);
}
