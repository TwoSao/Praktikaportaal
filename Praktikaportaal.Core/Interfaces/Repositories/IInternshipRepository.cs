using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;

namespace Praktikaportaal.Core.Interfaces.Repositories;

/// <summary>
/// Data access for <see cref="Internship"/>. Returned internships include company, skills and applications.
/// </summary>
public interface IInternshipRepository
{
    Task<Internship?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Published listings matching the filters, sorted, one page + total count.</summary>
    Task<(IReadOnlyList<Internship> Items, int TotalCount)> SearchPublishedAsync(InternshipQueryParameters query, CancellationToken ct = default);

    /// <summary>Distinct fields and locations of published listings.</summary>
    Task<(IReadOnlyList<string> Fields, IReadOnlyList<string> Locations)> GetPublishedFilterValuesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Internship>> GetByCompanyAsync(Guid companyProfileId, CancellationToken ct = default);
    Task AddAsync(Internship internship, CancellationToken ct = default);
}
