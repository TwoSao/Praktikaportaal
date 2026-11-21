using System.ComponentModel.DataAnnotations;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Web.Models;

/// <summary>One row of the skill picker: a checkbox plus the self-assessed level.</summary>
public class SkillSelectionViewModel
{
    public Guid SkillId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Selected { get; set; }
    public SkillLevel Level { get; set; } = SkillLevel.Beginner;
}

/// <summary>The student's profile form. Property names mirror <c>UpdateStudentProfileDto</c> so API errors land on the right field.</summary>
public class ProfileViewModel
{
    [Display(Name = "E-post")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sisesta eesnimi")]
    [StringLength(100)]
    [Display(Name = "Eesnimi")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sisesta perekonnanimi")]
    [StringLength(100)]
    [Display(Name = "Perekonnanimi")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sisesta kooli nimi")]
    [StringLength(200)]
    [Display(Name = "Kool")]
    public string SchoolName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sisesta õppekava")]
    [StringLength(200)]
    [Display(Name = "Õppekava")]
    public string StudyProgram { get; set; } = string.Empty;

    [Range(1, 6, ErrorMessage = "Kursus peab olema 1 kuni 6")]
    [Display(Name = "Kursus")]
    public int Course { get; set; } = 1;

    [StringLength(2000, ErrorMessage = "Tutvustus võib olla kuni 2000 märki")]
    [Display(Name = "Endast")]
    public string? Description { get; set; }

    [Phone(ErrorMessage = "Telefoninumber ei ole korrektne")]
    [StringLength(30)]
    [Display(Name = "Telefon")]
    public string? PhoneNumber { get; set; }

    [StringLength(150)]
    [Display(Name = "Asukoht")]
    public string? Location { get; set; }

    public List<SkillSelectionViewModel> Skills { get; set; } = [];

    /// <summary>Current CV (read-only; uploaded with its own form).</summary>
    public CvDto? Cv { get; set; }
}

public class ApplyViewModel
{
    public Guid InternshipId { get; set; }

    [StringLength(5000, ErrorMessage = "Kaaskiri võib olla kuni 5000 märki")]
    [Display(Name = "Kaaskiri")]
    public string? CoverLetter { get; set; }

    // Shown on the page, not posted.
    public InternshipDto? Internship { get; set; }
    public CvDto? Cv { get; set; }
}

public class StudentApplicationsViewModel
{
    public required PagedResult<ApplicationDto> Result { get; init; }
    public ApplicationStatus? Status { get; init; }
}

public class RecommendationsViewModel
{
    public required PagedResult<RecommendationDto> Result { get; init; }
    public double MinOverlap { get; init; }
    public bool HasSkills { get; init; }
}

public class NotificationsViewModel
{
    public required NotificationListDto Result { get; init; }
    public bool UnreadOnly { get; init; }
}
