namespace Praktikaportaal.Core.Entities;

/// <summary>
/// Student-specific profile linked one-to-one to a User with Role == Student.
/// Contains academic and contact details used when applying to internships.
/// </summary>
public class StudentProfile : BaseEntity
{
    /// <summary>FK to the owning User (1-to-1).</summary>
    public Guid UserId { get; set; }

    /// <summary>Name of the educational institution. Max 200 chars.</summary>
    public string SchoolName { get; set; } = string.Empty;

    /// <summary>Curriculum or speciality name. Max 200 chars.</summary>
    public string StudyProgram { get; set; } = string.Empty;

    /// <summary>Current year of study (e.g. 1–4).</summary>
    public int Course { get; set; }

    /// <summary>Free-text self-description. Max 2000 chars.</summary>
    public string? Description { get; set; }

    /// <summary>Contact phone number. Max 30 chars.</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>City or region where the student is located. Max 150 chars.</summary>
    public string? Location { get; set; }

    // ── Navigation properties ──────────────────────────────────────────────

    public User User { get; set; } = null!;

    public ICollection<StudentSkill> StudentSkills { get; set; } = [];
    public ICollection<Application> Applications { get; set; } = [];
    public ICollection<CV> CVs { get; set; } = [];
    public ICollection<SavedInternship> SavedInternships { get; set; } = [];
}
