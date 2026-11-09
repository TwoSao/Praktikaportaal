using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Core.Interfaces;

/// <summary>
/// Internship listing business logic.
/// Managing a listing (update, delete, status) is allowed for the owning company and for admins (moderation).
/// </summary>
public interface IInternshipService
{
    Task<IReadOnlyList<InternshipDto>> GetPublishedInternshipsAsync(string? search = null, bool? isRemote = null, CancellationToken ct = default);

    /// <summary>
    /// Returns a listing. Non-published listings are visible only to the owning company and admins;
    /// for everybody else they are reported as not found.
    /// </summary>
    Task<InternshipDto> GetInternshipByIdAsync(Guid id, CurrentUser? caller = null, CancellationToken ct = default);

    Task<IReadOnlyList<InternshipDto>> GetCompanyInternshipsAsync(Guid companyProfileId, CancellationToken ct = default);

    Task<InternshipDto> CreateInternshipAsync(Guid companyProfileId, CreateInternshipDto dto, CancellationToken ct = default);

    Task<InternshipDto> UpdateInternshipAsync(Guid internshipId, UpdateInternshipDto dto, CurrentUser caller, CancellationToken ct = default);

    /// <summary>Soft-deletes the listing; existing applications are kept for history.</summary>
    Task DeleteInternshipAsync(Guid internshipId, CurrentUser caller, CancellationToken ct = default);

    Task UpdateInternshipStatusAsync(Guid internshipId, InternshipStatus newStatus, CurrentUser caller, CancellationToken ct = default);
}
