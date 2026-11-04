using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Core.Interfaces;

public interface IInternshipService
{
    Task<IReadOnlyList<InternshipDto>> GetPublishedInternshipsAsync(string? search = null, bool? isRemote = null, CancellationToken ct = default);
    Task<InternshipDto?> GetInternshipByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<InternshipDto>> GetCompanyInternshipsAsync(Guid companyProfileId, CancellationToken ct = default);
    Task<InternshipDto> CreateInternshipAsync(Guid companyProfileId, CreateInternshipDto dto, CancellationToken ct = default);
    Task<bool> UpdateInternshipStatusAsync(Guid internshipId, Guid companyProfileId, InternshipStatus newStatus, CancellationToken ct = default);
}

