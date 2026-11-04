namespace Praktikaportaal.Core.Entities;

/// <summary>
/// Represents an internship listing bookmarked / saved by a student for later review.
/// Composite PK: (StudentProfileId, InternshipId) — enforced in Infrastructure.
/// A student can save the same internship only once.
/// </summary>
public class SavedInternship
{
    public Guid StudentProfileId { get; set; }
    public Guid InternshipId { get; set; }

    /// <summary>UTC timestamp when the student saved the listing.</summary>
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation properties ──────────────────────────────────────────────

    public StudentProfile StudentProfile { get; set; } = null!;
    public Internship Internship { get; set; } = null!;
}
