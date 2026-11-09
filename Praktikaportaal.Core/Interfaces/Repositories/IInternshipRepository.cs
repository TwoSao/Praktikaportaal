using Praktikaportaal.Core.Entities;

namespace Praktikaportaal.Core.Interfaces.Repositories;

/// <summary>
/// Data access for <see cref="Internship"/>. Returned internships include company, skills and applications.
/// </summary>
public interface IInternshipRepository
{
    Task<Internship?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Internship>> GetPublishedAsync(string? search, bool? isRemote, CancellationToken ct = default);
    Task<IReadOnlyList<Internship>> GetByCompanyAsync(Guid companyProfileId, CancellationToken ct = default);
    Task<bool> IsOwnedByCompanyAsync(Guid internshipId, Guid companyProfileId, CancellationToken ct = default);
    Task AddAsync(Internship internship, CancellationToken ct = default);
}
