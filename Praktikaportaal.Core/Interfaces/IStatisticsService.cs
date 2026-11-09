using Praktikaportaal.Core.DTOs;

namespace Praktikaportaal.Core.Interfaces;

/// <summary>Application statistics for the admin dashboard.</summary>
public interface IStatisticsService
{
    /// <summary>
    /// Totals, applications by status, top internships, applications by field and a time series,
    /// for applications submitted in the optional date range.
    /// </summary>
    Task<ApplicationStatisticsDto> GetApplicationStatisticsAsync(StatisticsQueryParameters query, CancellationToken ct = default);
}
