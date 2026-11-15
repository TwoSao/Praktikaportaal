using Praktikaportaal.Core.DTOs;

namespace Praktikaportaal.Core.Interfaces.Repositories;

/// <summary>
/// Read-only aggregate queries over applications (done in the database).
/// The range is [from, toExclusive) on the application's submission time; null means unbounded.
/// Applications of soft-deleted internships are still counted (history is kept).
/// </summary>
public interface IStatisticsRepository
{
    /// <summary>Counts per status that actually occur (missing statuses are added by the service).</summary>
    Task<IReadOnlyList<StatusCountDto>> CountByStatusAsync(DateTime? from, DateTime? toExclusive, CancellationToken ct = default);

    /// <summary>Internships with the most applications, highest first.</summary>
    Task<IReadOnlyList<InternshipCountDto>> GetTopInternshipsAsync(DateTime? from, DateTime? toExclusive, int take, CancellationToken ct = default);

    Task<IReadOnlyList<FieldCountDto>> CountByFieldAsync(DateTime? from, DateTime? toExclusive, CancellationToken ct = default);

    /// <summary>Submission times of the applications in the range, for bucketing by period.</summary>
    Task<IReadOnlyList<DateTime>> GetApplicationDatesAsync(DateTime? from, DateTime? toExclusive, CancellationToken ct = default);

    Task<PortalOverviewDto> GetOverviewAsync(CancellationToken ct = default);
}
