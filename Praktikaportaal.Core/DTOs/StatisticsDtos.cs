using System.ComponentModel.DataAnnotations;
using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Core.DTOs;

/// <summary>How the "applications per period" series is bucketed.</summary>
public enum StatisticsPeriod
{
    Day,
    /// <summary>ISO weeks, starting on Monday.</summary>
    Week,
    Month
}

/// <summary>Query string of GET /api/admin/statistics.</summary>
public class StatisticsQueryParameters
{
    /// <summary>Count only applications submitted on or after this date (UTC). Default: no lower limit.</summary>
    /// <example>2026-10-01</example>
    public DateTime? From { get; set; }

    /// <summary>
    /// Count only applications submitted up to and including this date (UTC).
    /// A date without a time includes that whole day. Default: no upper limit.
    /// </summary>
    /// <example>2026-11-30</example>
    public DateTime? To { get; set; }

    /// <summary>Bucket size of the time series.</summary>
    /// <example>Week</example>
    public StatisticsPeriod Period { get; set; } = StatisticsPeriod.Month;

    /// <summary>How many internships to list in <c>topInternships</c> (1–50).</summary>
    /// <example>10</example>
    [Range(1, 50)]
    public int Top { get; set; } = 10;
}

public class StatusCountDto
{
    /// <example>Submitted</example>
    public ApplicationStatus Status { get; set; }

    /// <example>7</example>
    public int Count { get; set; }
}

public class InternshipCountDto
{
    public Guid InternshipId { get; set; }

    /// <example>Junior .NET Developer Intern</example>
    public string Title { get; set; } = string.Empty;

    /// <example>Nordic Software OÜ</example>
    public string CompanyName { get; set; } = string.Empty;

    /// <example>4</example>
    public int Count { get; set; }
}

public class FieldCountDto
{
    /// <example>IT</example>
    public string Field { get; set; } = string.Empty;

    /// <example>9</example>
    public int Count { get; set; }
}

public class PeriodCountDto
{
    /// <summary>First day of the day / ISO week / month.</summary>
    /// <example>2026-11-02</example>
    public DateTime PeriodStart { get; set; }

    /// <example>5</example>
    public int Count { get; set; }
}

/// <summary>Whole-portal totals, independent of the date filter.</summary>
public class PortalOverviewDto
{
    public int Students { get; set; }
    public int Companies { get; set; }
    public int PublishedInternships { get; set; }
}

/// <summary>Application statistics for the admin dashboard.</summary>
public class ApplicationStatisticsDto
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    /// <summary>Applications in the selected range.</summary>
    /// <example>18</example>
    public int TotalApplications { get; set; }

    /// <summary>Every status is listed, with 0 when there are none.</summary>
    public List<StatusCountDto> ByStatus { get; set; } = [];

    /// <summary>Internships with the most applications, highest first.</summary>
    public List<InternshipCountDto> TopInternships { get; set; } = [];

    /// <summary>Applications per internship field, highest first.</summary>
    public List<FieldCountDto> ByField { get; set; } = [];

    public StatisticsPeriod Period { get; set; }

    /// <summary>Applications per period, oldest first, without gaps (empty periods have count 0).</summary>
    public List<PeriodCountDto> ByPeriod { get; set; } = [];

    public PortalOverviewDto Overview { get; set; } = new();
}
