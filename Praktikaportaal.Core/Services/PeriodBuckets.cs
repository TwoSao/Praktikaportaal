using Praktikaportaal.Core.DTOs;

namespace Praktikaportaal.Core.Services;

/// <summary>Helpers to group timestamps into day / ISO-week / month buckets.</summary>
public static class PeriodBuckets
{
    /// <summary>First day of the bucket that contains <paramref name="date"/>.</summary>
    public static DateTime Start(DateTime date, StatisticsPeriod period) => period switch
    {
        StatisticsPeriod.Day => date.Date,
        StatisticsPeriod.Week => date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7)), // Monday
        StatisticsPeriod.Month => new DateTime(date.Year, date.Month, 1, 0, 0, 0, date.Kind),
        _ => throw new ArgumentOutOfRangeException(nameof(period))
    };

    /// <summary>Start of the bucket that follows the one starting at <paramref name="start"/>.</summary>
    public static DateTime Next(DateTime start, StatisticsPeriod period) => period switch
    {
        StatisticsPeriod.Day => start.AddDays(1),
        StatisticsPeriod.Week => start.AddDays(7),
        StatisticsPeriod.Month => start.AddMonths(1),
        _ => throw new ArgumentOutOfRangeException(nameof(period))
    };

    /// <summary>
    /// Counts the dates per bucket and fills empty buckets between the first and last one with 0.
    /// <paramref name="rangeStart"/>/<paramref name="rangeEnd"/> extend the series to the requested range.
    /// </summary>
    public static List<PeriodCountDto> Count(
        IEnumerable<DateTime> dates, StatisticsPeriod period, DateTime? rangeStart = null, DateTime? rangeEnd = null)
    {
        var counts = dates.GroupBy(d => Start(d, period)).ToDictionary(g => g.Key, g => g.Count());

        // Bounds: the requested range if given, otherwise the first/last bucket that has data.
        DateTime? first = rangeStart.HasValue ? Start(rangeStart.Value, period) : counts.Count > 0 ? counts.Keys.Min() : null;
        DateTime? last = rangeEnd.HasValue ? Start(rangeEnd.Value, period) : counts.Count > 0 ? counts.Keys.Max() : null;
        if (first == null || last == null)
            return [];

        var result = new List<PeriodCountDto>();
        for (var current = first.Value; current <= last.Value; current = Next(current, period))
            result.Add(new PeriodCountDto { PeriodStart = current, Count = counts.GetValueOrDefault(current) });

        return result;
    }

    /// <summary>Number of buckets between two dates, inclusive.</summary>
    public static int CountBuckets(DateTime from, DateTime to, StatisticsPeriod period)
    {
        int n = 0;
        for (var current = Start(from, period); current <= to; current = Next(current, period))
            n++;
        return n;
    }
}
