using System.ComponentModel.DataAnnotations;
using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Core.DTOs;

/// <summary>New internship listing (Company).</summary>
public class CreateInternshipDto : IValidatableObject
{
    /// <example>Junior .NET Developer Intern</example>
    [Required, StringLength(200, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    /// <example>Join our team and build ASP.NET Core APIs with a mentor.</example>
    [Required, StringLength(5000, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Professional field used for search.</summary>
    /// <example>IT</example>
    [Required, MaxLength(100)]
    public string Field { get; set; } = string.Empty;

    /// <example>C#, SQL and Git basics.</example>
    [MaxLength(3000)]
    public string? Requirements { get; set; }

    /// <example>Tallinn</example>
    [MaxLength(150)]
    public string? Location { get; set; }

    /// <example>2027-02-01</example>
    public DateTime? StartDate { get; set; }

    /// <example>2027-04-01</example>
    public DateTime? EndDate { get; set; }

    /// <example>2027-01-15</example>
    public DateTime? ApplicationDeadline { get; set; }

    /// <example>false</example>
    public bool IsRemote { get; set; }

    /// <summary>Required skill ids (see GET /api/skills).</summary>
    public List<Guid>? SkillIds { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate.HasValue && EndDate.HasValue && EndDate < StartDate)
            yield return new ValidationResult("End date must be after start date.", [nameof(EndDate)]);

        if (ApplicationDeadline.HasValue && ApplicationDeadline.Value.Date < DateTime.UtcNow.Date)
            yield return new ValidationResult("Application deadline cannot be in the past.", [nameof(ApplicationDeadline)]);

        if (ApplicationDeadline.HasValue && StartDate.HasValue && ApplicationDeadline > StartDate)
            yield return new ValidationResult("Application deadline must be before the start date.", [nameof(ApplicationDeadline)]);
    }
}

/// <summary>Internship listing as returned by the API.</summary>
public class InternshipDto
{
    public Guid Id { get; set; }
    public Guid CompanyProfileId { get; set; }

    /// <example>Nordic Software OÜ</example>
    public string CompanyName { get; set; } = string.Empty;

    /// <example>Tallinn, Ülemiste City</example>
    public string? CompanyLocation { get; set; }

    /// <example>https://nordicsoftware.example</example>
    public string? CompanyWebsite { get; set; }

    /// <example>Junior .NET Developer Intern</example>
    public string Title { get; set; } = string.Empty;

    /// <example>Join our team and build ASP.NET Core APIs with a mentor.</example>
    public string Description { get; set; } = string.Empty;

    /// <example>IT</example>
    public string Field { get; set; } = string.Empty;

    public string? Requirements { get; set; }

    /// <example>Tallinn</example>
    public string? Location { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? ApplicationDeadline { get; set; }
    public bool IsRemote { get; set; }

    /// <example>Published</example>
    public InternshipStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Names of required skills.</summary>
    public List<string> Skills { get; set; } = [];

    /// <summary>Number of active (non-withdrawn) applications.</summary>
    /// <example>3</example>
    public int ApplicationsCount { get; set; }
}

public class UpdateInternshipStatusDto
{
    /// <example>Closed</example>
    [Required, EnumDataType(typeof(InternshipStatus))]
    public InternshipStatus Status { get; set; }
}
