using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Core.Entities;

/// <summary>
/// An internship opportunity published by a company.
/// Status transitions: Draft → Published → Closed → Archived.
/// Soft-delete is inherited; physical deletion is avoided to preserve
/// existing Application history.
/// </summary>
public class Internship : BaseEntity
{
    /// <summary>FK to the company that owns this listing.</summary>
    public Guid CompanyProfileId { get; set; }

    /// <summary>Short, descriptive title of the position. Max 200 chars.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Full description of the internship. Max 5000 chars.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Skill or qualification requirements. Max 3000 chars.</summary>
    public string? Requirements { get; set; }

    /// <summary>City / region where the internship takes place. Max 150 chars.</summary>
    public string? Location { get; set; }

    /// <summary>Planned start date (UTC date, time portion is midnight).</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>Planned end date (UTC date, time portion is midnight).</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Deadline for submitting applications (UTC).
    /// Applications cannot be accepted after this date.
    /// </summary>
    public DateTime? ApplicationDeadline { get; set; }

    /// <summary>Whether the internship can be performed remotely.</summary>
    public bool IsRemote { get; set; } = false;

    /// <summary>Current lifecycle status of the listing.</summary>
    public InternshipStatus Status { get; set; } = InternshipStatus.Draft;

    // ── Navigation properties ──────────────────────────────────────────────

    public CompanyProfile CompanyProfile { get; set; } = null!;

    public ICollection<InternshipSkill> InternshipSkills { get; set; } = [];
    public ICollection<Application> Applications { get; set; } = [];
    public ICollection<SavedInternship> SavedByStudents { get; set; } = [];
}
