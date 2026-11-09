using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Core.Entities;

/// <summary>
/// A student's application to an internship.
///
/// Uniqueness rule: a student may not have more than one non-Withdrawn
/// application for the same internship at a time. After Withdrawn the
/// student is permitted to apply again (enforced at the service layer, not here).
///
/// Soft-delete is intentionally avoided for applications: the record must
/// remain for audit purposes even if the student account is deactivated.
/// </summary>
public class Application : BaseEntity
{
    /// <summary>FK to the applying student's profile.</summary>
    public Guid StudentProfileId { get; set; }

    /// <summary>FK to the targeted internship.</summary>
    public Guid InternshipId { get; set; }

    /// <summary>Cover letter text submitted with the application. Max 5000 chars.</summary>
    public string? CoverLetter { get; set; }

    /// <summary>Current processing status of the application.</summary>
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Submitted;

    /// <summary>UTC timestamp when the application was submitted (same as CreatedAt, kept explicit for readability).</summary>
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation properties ──────────────────────────────────────────────

    public StudentProfile StudentProfile { get; set; } = null!;
    public Internship Internship { get; set; } = null!;
}
