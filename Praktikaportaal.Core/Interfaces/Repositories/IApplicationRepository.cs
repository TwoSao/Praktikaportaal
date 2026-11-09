using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;

namespace Praktikaportaal.Core.Interfaces.Repositories;

/// <summary>
/// Data access for <see cref="Application"/>. Returned applications include internship, company and student user.
/// </summary>
public interface IApplicationRepository
{
    Task<Application?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Student's applications, newest first, filtered and paged.</summary>
    Task<(IReadOnlyList<Application> Items, int TotalCount)> GetByStudentAsync(Guid studentProfileId, ApplicationQueryParameters query, CancellationToken ct = default);

    /// <summary>Applications for an internship, newest first, filtered and paged.</summary>
    Task<(IReadOnlyList<Application> Items, int TotalCount)> GetByInternshipAsync(Guid internshipId, ApplicationQueryParameters query, CancellationToken ct = default);

    /// <summary>True if the student has a non-withdrawn application for the internship.</summary>
    Task<bool> HasActiveApplicationAsync(Guid studentProfileId, Guid internshipId, CancellationToken ct = default);

    Task AddAsync(Application application, CancellationToken ct = default);
}
