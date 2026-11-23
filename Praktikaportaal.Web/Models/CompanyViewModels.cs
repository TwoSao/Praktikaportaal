using System.ComponentModel.DataAnnotations;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Web.Models;

/// <summary>
/// Create/edit form of an internship. Property names mirror <c>CreateInternshipDto</c>, so API errors land on the right field.
/// The same rules as the API are checked here (with Estonian messages) before anything is sent.
/// </summary>
public class InternshipFormViewModel : IValidatableObject
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Sisesta pealkiri")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Pealkiri peab olema 3 kuni 200 märki")]
    [Display(Name = "Pealkiri")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lisa kirjeldus")]
    [StringLength(5000, MinimumLength = 10, ErrorMessage = "Kirjeldus peab olema 10 kuni 5000 märki")]
    [Display(Name = "Kirjeldus")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vali või sisesta valdkond")]
    [StringLength(100)]
    [Display(Name = "Valdkond")]
    public string Field { get; set; } = string.Empty;

    [StringLength(3000, ErrorMessage = "Nõuded võivad olla kuni 3000 märki")]
    [Display(Name = "Nõuded")]
    public string? Requirements { get; set; }

    [StringLength(150)]
    [Display(Name = "Asukoht")]
    public string? Location { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Praktika algus")]
    public DateTime? StartDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Praktika lõpp")]
    public DateTime? EndDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Avalduste tähtaeg")]
    public DateTime? ApplicationDeadline { get; set; }

    [Display(Name = "Kaugtöö võimalik")]
    public bool IsRemote { get; set; }

    /// <summary>Ids of the required skills (checkboxes named SkillIds).</summary>
    public List<Guid> SkillIds { get; set; } = [];

    // Shown by the form, not posted.
    public List<SkillDto> AllSkills { get; set; } = [];
    public List<string> KnownFields { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate.HasValue && EndDate.HasValue && EndDate < StartDate)
            yield return new ValidationResult("Lõppkuupäev peab olema pärast alguskuupäeva", [nameof(EndDate)]);

        // An existing listing may keep a deadline that has since passed (the company only fixes a typo),
        // so "not in the past" is enforced for new listings only.
        if (!Id.HasValue && ApplicationDeadline.HasValue && ApplicationDeadline.Value.Date < DateTime.UtcNow.Date)
            yield return new ValidationResult("Avalduste tähtaeg ei tohi olla minevikus", [nameof(ApplicationDeadline)]);

        if (ApplicationDeadline.HasValue && StartDate.HasValue && ApplicationDeadline > StartDate)
            yield return new ValidationResult("Avalduste tähtaeg peab olema enne praktika algust", [nameof(ApplicationDeadline)]);
    }

    public CreateInternshipDto ToCreateDto() => new()
    {
        Title = Title.Trim(),
        Description = Description.Trim(),
        Field = Field.Trim(),
        Requirements = Blank(Requirements),
        Location = Blank(Location),
        StartDate = StartDate,
        EndDate = EndDate,
        ApplicationDeadline = ApplicationDeadline,
        IsRemote = IsRemote,
        SkillIds = SkillIds.Distinct().ToList()
    };

    public UpdateInternshipDto ToUpdateDto() => new()
    {
        Title = Title.Trim(),
        Description = Description.Trim(),
        Field = Field.Trim(),
        Requirements = Blank(Requirements),
        Location = Blank(Location),
        StartDate = StartDate,
        EndDate = EndDate,
        ApplicationDeadline = ApplicationDeadline,
        IsRemote = IsRemote,
        SkillIds = SkillIds.Distinct().ToList()
    };

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public class CompanyDashboardViewModel
{
    public required IReadOnlyList<InternshipDto> Internships { get; init; }

    public int Published => Internships.Count(i => i.Status == InternshipStatus.Published);
    public int TotalApplications => Internships.Sum(i => i.ApplicationsCount);
}

public class ApplicantsViewModel
{
    public required InternshipDto Internship { get; init; }
    public required PagedResult<ApplicationDto> Result { get; init; }
    public ApplicationStatus? Status { get; init; }
}
