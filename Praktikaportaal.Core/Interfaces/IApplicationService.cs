using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Core.Interfaces;

/// <summary>
/// Application (student → internship) business logic.
/// Errors are reported with exceptions from <c>Praktikaportaal.Core.Exceptions</c>.
/// </summary>
public interface IApplicationService
{
    /// <summary>Student applies to a published internship. Throws Conflict on a duplicate active application.</summary>
    Task<ApplicationDto> ApplyAsync(Guid studentProfileId, Guid internshipId, CreateApplicationDto dto, CancellationToken ct = default);

    /// <summary>Returns one application if the caller is its student, the owning company or an admin.</summary>
    Task<ApplicationDto> GetByIdAsync(Guid applicationId, CurrentUser caller, CancellationToken ct = default);

    Task<IReadOnlyList<ApplicationDto>> GetStudentApplicationsAsync(Guid studentProfileId, CancellationToken ct = default);

    /// <summary>Applications for an internship; only the owning company (or an admin) may see them.</summary>
    Task<IReadOnlyList<ApplicationDto>> GetInternshipApplicationsAsync(Guid internshipId, CurrentUser caller, CancellationToken ct = default);

    Task UpdateApplicationStatusAsync(Guid applicationId, Guid companyProfileId, ApplicationStatus newStatus, CancellationToken ct = default);

    Task WithdrawApplicationAsync(Guid applicationId, Guid studentProfileId, CancellationToken ct = default);
}
