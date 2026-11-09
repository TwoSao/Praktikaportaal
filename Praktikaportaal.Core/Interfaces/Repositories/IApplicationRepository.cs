using Praktikaportaal.Core.Entities;

namespace Praktikaportaal.Core.Interfaces.Repositories;

/// <summary>
/// Data access for <see cref="Application"/>. Returned applications include internship, company and student user.
/// </summary>
public interface IApplicationRepository
{
    Task<Application?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Application>> GetByStudentAsync(Guid studentProfileId, CancellationToken ct = default);
    Task<IReadOnlyList<Application>> GetByInternshipAsync(Guid internshipId, CancellationToken ct = default);

    /// <summary>True if the student has a non-withdrawn application for the internship.</summary>
    Task<bool> HasActiveApplicationAsync(Guid studentProfileId, Guid internshipId, CancellationToken ct = default);

    Task AddAsync(Application application, CancellationToken ct = default);
}
