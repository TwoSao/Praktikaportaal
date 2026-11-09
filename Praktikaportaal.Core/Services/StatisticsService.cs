using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Core.Exceptions;
using Praktikaportaal.Core.Interfaces;
using Praktikaportaal.Core.Interfaces.Repositories;

namespace Praktikaportaal.Core.Services;

public class StatisticsService : IStatisticsService
{
    /// <summary>Upper limit for the time series so a huge range with daily buckets cannot flood the response.</summary>
    public const int MaxPeriodBuckets = 400;

    private readonly IStatisticsRepository _statistics;

    public StatisticsService(IStatisticsRepository statistics)
    {
        _statistics = statistics;
    }

    public async Task<ApplicationStatisticsDto> GetApplicationStatisticsAsync(
        StatisticsQueryParameters query,
        CancellationToken ct = default)
    {
        if (query.From.HasValue && query.To.HasValue && query.To < query.From)
            throw new BusinessRuleException("'to' must not be earlier than 'from'.");

        if (query.From.HasValue && query.To.HasValue &&
            PeriodBuckets.CountBuckets(query.From.Value, query.To.Value, query.Period) > MaxPeriodBuckets)
        {
            throw new BusinessRuleException(
                $"The range is too long for {query.Period} buckets (max {MaxPeriodBuckets}). Use a larger period or a shorter range.");
        }

        var from = query.From;
        // A bare date means "that whole day": the upper bound is exclusive at the next midnight.
        DateTime? toExclusive = query.To.HasValue
            ? (query.To.Value.TimeOfDay == TimeSpan.Zero ? query.To.Value.AddDays(1) : query.To.Value.AddTicks(1))
            : null;

        // These queries share one DbContext, so they run one after another.
        var byStatus = await _statistics.CountByStatusAsync(from, toExclusive, ct);
        var topInternships = await _statistics.GetTopInternshipsAsync(from, toExclusive, query.Top, ct);
        var byField = await _statistics.CountByFieldAsync(from, toExclusive, ct);
        var dates = await _statistics.GetApplicationDatesAsync(from, toExclusive, ct);
        var overview = await _statistics.GetOverviewAsync(ct);

        var series = PeriodBuckets.Count(dates, query.Period, from, query.To);
        if (series.Count > MaxPeriodBuckets)
            throw new BusinessRuleException(
                $"The data spans more than {MaxPeriodBuckets} {query.Period} buckets. Narrow the range with 'from'/'to' or use a larger period.");

        return new ApplicationStatisticsDto
        {
            From = query.From,
            To = query.To,
            TotalApplications = byStatus.Sum(s => s.Count),
            // List every status, in lifecycle order, even if there are none.
            ByStatus = Enum.GetValues<ApplicationStatus>()
                .Select(status => new StatusCountDto { Status = status, Count = byStatus.FirstOrDefault(s => s.Status == status)?.Count ?? 0 })
                .ToList(),
            TopInternships = topInternships.ToList(),
            ByField = byField.ToList(),
            Period = query.Period,
            ByPeriod = series,
            Overview = overview
        };
    }
}
