namespace Praktikaportaal.Core.Entities;

/// <summary>
/// A reusable skill tag that can be attached to both student profiles and internship listings.
/// </summary>
public class Skill : BaseEntity
{
    /// <summary>Human-readable skill name (e.g. "C#", "SQL", "Project Management"). Max 100 chars. Must be unique.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description or context for the skill. Max 500 chars.</summary>
    public string? Description { get; set; }

    // ── Navigation properties ──────────────────────────────────────────────

    public ICollection<StudentSkill> StudentSkills { get; set; } = [];
    public ICollection<InternshipSkill> InternshipSkills { get; set; } = [];
}
