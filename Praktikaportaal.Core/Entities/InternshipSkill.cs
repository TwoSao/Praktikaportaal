namespace Praktikaportaal.Core.Entities;

/// <summary>
/// Join entity for the many-to-many relationship between Internship and Skill.
/// Represents a skill requirement for an internship position.
/// Composite PK: (InternshipId, SkillId) — enforced via EF configuration in Infrastructure.
/// </summary>
public class InternshipSkill
{
    public Guid InternshipId { get; set; }
    public Guid SkillId { get; set; }

    /// <summary>
    /// Optional minimum required level for this skill.
    /// Null means the level is unspecified / any level accepted.
    /// </summary>
    public Praktikaportaal.Core.Enums.SkillLevel? RequiredLevel { get; set; }

    // ── Navigation properties ──────────────────────────────────────────────

    public Internship Internship { get; set; } = null!;
    public Skill Skill { get; set; } = null!;
}
