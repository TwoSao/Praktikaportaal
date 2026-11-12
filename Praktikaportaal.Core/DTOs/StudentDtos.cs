using System.ComponentModel.DataAnnotations;
using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Core.DTOs;

/// <summary>A skill of a student with the self-assessed level.</summary>
public class StudentSkillDto
{
    public Guid SkillId { get; set; }

    /// <example>C#</example>
    public string Name { get; set; } = string.Empty;

    /// <example>Intermediate</example>
    public SkillLevel Level { get; set; }
}

/// <summary>Skill entry in a profile update.</summary>
public class StudentSkillInputDto
{
    /// <summary>Skill id from GET /api/skills.</summary>
    [Required]
    public Guid SkillId { get; set; }

    /// <example>Intermediate</example>
    [EnumDataType(typeof(SkillLevel))]
    public SkillLevel Level { get; set; } = SkillLevel.Beginner;
}

/// <summary>Student profile as returned by the API.</summary>
public class StudentProfileDto
{
    /// <summary>StudentProfile id (the "profile_id" claim).</summary>
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <example>mari.tamm@student.ee</example>
    public string Email { get; set; } = string.Empty;

    /// <example>Mari</example>
    public string FirstName { get; set; } = string.Empty;

    /// <example>Tamm</example>
    public string LastName { get; set; } = string.Empty;

    /// <example>Tallinna Tööstushariduskeskus</example>
    public string SchoolName { get; set; } = string.Empty;

    /// <example>Software Development</example>
    public string StudyProgram { get; set; } = string.Empty;

    /// <example>3</example>
    public int Course { get; set; }

    public string? Description { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Location { get; set; }

    public List<StudentSkillDto> Skills { get; set; } = [];

    /// <summary>True when the student has uploaded a CV.</summary>
    public bool HasCv { get; set; }
}

/// <summary>
/// Full update of the student's own profile (PUT semantics: the skill list replaces the existing one).
/// </summary>
public class UpdateStudentProfileDto
{
    /// <example>Mari</example>
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    /// <example>Tamm</example>
    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    /// <example>Tallinna Tööstushariduskeskus</example>
    [Required, MaxLength(200)]
    public string SchoolName { get; set; } = string.Empty;

    /// <example>Software Development</example>
    [Required, MaxLength(200)]
    public string StudyProgram { get; set; } = string.Empty;

    /// <example>3</example>
    [Range(1, 6)]
    public int Course { get; set; }

    /// <example>Looking for a backend internship in spring 2027.</example>
    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <example>+372 5555 1234</example>
    [Phone, MaxLength(30)]
    public string? PhoneNumber { get; set; }

    /// <example>Tallinn</example>
    [MaxLength(150)]
    public string? Location { get; set; }

    /// <summary>Complete list of the student's skills (max 50). Each skill id may appear once.</summary>
    [MaxLength(50)]
    public List<StudentSkillInputDto> Skills { get; set; } = [];
}
