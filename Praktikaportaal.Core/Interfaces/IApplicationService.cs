using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Core.Interfaces;

public interface IApplicationService
{
    Task<(bool Success, string? Error, ApplicationDto? Application)> ApplyAsync(Guid studentProfileId, CreateApplicationDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<ApplicationDto>> GetStudentApplicationsAsync(Guid studentProfileId, CancellationToken ct = default);
    Task<IReadOnlyList<ApplicationDto>> GetInternshipApplicationsAsync(Guid internshipId, Guid companyProfileId, CancellationToken ct = default);
    Task<bool> UpdateApplicationStatusAsync(Guid applicationId, Guid companyProfileId, ApplicationStatus newStatus, CancellationToken ct = default);
    Task<bool> WithdrawApplicationAsync(Guid applicationId, Guid studentProfileId, CancellationToken ct = default);
}
