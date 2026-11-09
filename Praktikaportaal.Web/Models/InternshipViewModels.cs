using System.ComponentModel.DataAnnotations;

namespace Praktikaportaal.Web.Models;

public class InternshipItemViewModel
{
    public Guid Id { get; set; }
    public Guid CompanyProfileId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyLocation { get; set; }
    public string? CompanyWebsite { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Requirements { get; set; }
    public string? Location { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? ApplicationDeadline { get; set; }
    public bool IsRemote { get; set; }
    public string Status { get; set; } = "Published";
    public DateTime CreatedAt { get; set; }
    public List<string> Skills { get; set; } = [];
    public int ApplicationsCount { get; set; }
}

public class CreateInternshipViewModel
{
    [Required(ErrorMessage = "Praktika pealkiri on kohustuslik")]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kirjeldus on kohustuslik")]
    [MaxLength(5000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(3000)]
    public string? Requirements { get; set; }

    [MaxLength(150)]
    public string? Location { get; set; }

    [DataType(DataType.Date)]
    public DateTime? StartDate { get; set; }

    [DataType(DataType.Date)]
    public DateTime? EndDate { get; set; }

    [DataType(DataType.Date)]
    public DateTime? ApplicationDeadline { get; set; }

    public bool IsRemote { get; set; }
}

public class ApplyViewModel
{
    public Guid InternshipId { get; set; }
    public string InternshipTitle { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;

    [MaxLength(5000)]
    [Display(Name = "Kaaskiri / Motivatsioonikiri")]
    public string? CoverLetter { get; set; }
}

