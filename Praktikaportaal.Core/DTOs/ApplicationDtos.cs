using System.ComponentModel.DataAnnotations;
using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Core.DTOs;

/// <summary>Body of POST /api/internships/{id}/applications.</summary>
public class CreateApplicationDto
{
    /// <summary>Optional motivation message to the company.</summary>
    /// <example>Hello! I am a 2nd year software development student and would love to join your team.</example>
    [MaxLength(5000)]
    public string? CoverLetter { get; set; }
}

/// <summary>Application as returned by the API (student contact data is shown to the owning company).</summary>
public class ApplicationDto
{
    public Guid Id { get; set; }
    public Guid InternshipId { get; set; }

    /// <example>Junior .NET Developer Intern</example>
    public string InternshipTitle { get; set; } = string.Empty;

    /// <example>Nordic Software OÜ</example>
    public string CompanyName { get; set; } = string.Empty;

    public Guid StudentProfileId { get; set; }

    /// <example>Mari Tamm</example>
    public string StudentName { get; set; } = string.Empty;

    /// <example>mari.tamm@student.ee</example>
    public string StudentEmail { get; set; } = string.Empty;

    public string? StudentPhoneNumber { get; set; }

    /// <example>Tallinna Tööstushariduskeskus</example>
    public string? SchoolName { get; set; }

    /// <example>Software Development</example>
    public string? StudyProgram { get; set; }

    /// <example>3</example>
    public int Course { get; set; }

    public string? CoverLetter { get; set; }

    /// <summary>True if the student has a CV; download it with GET /api/applications/{id}/cv.</summary>
    public bool HasCv { get; set; }

    /// <example>Submitted</example>
    public ApplicationStatus Status { get; set; }

    public DateTime AppliedAt { get; set; }
}

/// <summary>
/// Company status change. Allowed: Submitted→Viewed|Rejected, Viewed→Interview|Rejected,
/// Interview→Accepted|Rejected. Anything else returns 400.
/// </summary>
public class UpdateApplicationStatusDto
{
    /// <example>Interview</example>
    [Required, EnumDataType(typeof(ApplicationStatus))]
    public ApplicationStatus Status { get; set; }
}
