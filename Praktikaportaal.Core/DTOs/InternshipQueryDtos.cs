using System.ComponentModel.DataAnnotations;
using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Core.DTOs;

/// <summary>Sort order for the internship list.</summary>
public enum InternshipSort
{
    /// <summary>Newest first (default).</summary>
    Newest,
    Oldest,
    /// <summary>Closest application deadline first; listings without deadline last.</summary>
    Deadline,
    Title
}

/// <summary>Query string of GET /api/internships.</summary>
public class InternshipQueryParameters : PagingParameters
{
    /// <summary>Free-text search in title, description, field, location and company name.</summary>
    /// <example>developer</example>
    [MaxLength(100)]
    public string? Search { get; set; }

    /// <summary>Exact field (case-insensitive), see GET /api/internships/filters.</summary>
    /// <example>IT</example>
    [MaxLength(100)]
    public string? Field { get; set; }

    /// <summary>Location contains this text (case-insensitive).</summary>
    /// <example>Tallinn</example>
    [MaxLength(150)]
    public string? Location { get; set; }

    public bool? IsRemote { get; set; }

    /// <summary>Only listings created after this UTC time (used by the Worker for new listings).</summary>
    public DateTime? CreatedAfter { get; set; }

    /// <example>Newest</example>
    public InternshipSort Sort { get; set; } = InternshipSort.Newest;
}

/// <summary>Distinct values for filter dropdowns.</summary>
public class InternshipFiltersDto
{
    public IReadOnlyList<string> Fields { get; set; } = [];
    public IReadOnlyList<string> Locations { get; set; } = [];
}

/// <summary>Query string of application list endpoints.</summary>
public class ApplicationQueryParameters : PagingParameters
{
    /// <summary>Only applications with this status.</summary>
    /// <example>Submitted</example>
    public ApplicationStatus? Status { get; set; }
}
