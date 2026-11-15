using System.ComponentModel.DataAnnotations;

namespace Praktikaportaal.Core.DTOs;

/// <summary>Query string of GET /api/students/{id}/recommendations.</summary>
public class RecommendationQueryParameters : PagingParameters
{
    /// <summary>Only internships created after this UTC time (the Worker passes the time of its last run).</summary>
    public DateTime? Since { get; set; }

    /// <summary>Minimum overlap in percent (0–100). Default 1 = at least one matching skill.</summary>
    /// <example>50</example>
    [Range(0, 100)]
    public double MinOverlap { get; set; } = 1;
}

/// <summary>An internship recommended to a student with the skill overlap.</summary>
public class RecommendationDto
{
    public InternshipDto Internship { get; set; } = new();

    /// <summary>Share of the required skills the student has, 0–100.</summary>
    /// <example>75</example>
    public double OverlapPercent { get; set; }

    /// <summary>Required skills the student has.</summary>
    public List<string> MatchedSkills { get; set; } = [];

    /// <summary>Required skills the student is missing.</summary>
    public List<string> MissingSkills { get; set; } = [];
}
