using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Core.Entities;

/// <summary>
/// Join entity for the many-to-many relationship between StudentProfile and Skill.
/// Carries the student's self-assessed proficiency level for a given skill.
/// Composite PK: (StudentProfileId, SkillId) — enforced via EF configuration in Infrastructure.
/// </summary>
public class StudentSkill
{
    public Guid StudentProfileId { get; set; }
    public Guid SkillId { get; set; }

    /// <summary>Student's proficiency level for this skill.</summary>
    public SkillLevel Level { get; set; } = SkillLevel.Beginner;

    // ── Navigation properties ──────────────────────────────────────────────

    public StudentProfile StudentProfile { get; set; } = null!;
    public Skill Skill { get; set; } = null!;
}
