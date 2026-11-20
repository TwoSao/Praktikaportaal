using System.ComponentModel.DataAnnotations;

namespace Praktikaportaal.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Sisesta e-posti aadress")]
    [EmailAddress(ErrorMessage = "E-posti aadress ei ole korrektne")]
    [Display(Name = "E-post")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sisesta parool")]
    [DataType(DataType.Password)]
    [Display(Name = "Parool")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

/// <summary>Fields shared by both registration forms. Property names match the API's DTOs so API errors map onto them.</summary>
public abstract class RegisterViewModel
{
    [Required(ErrorMessage = "Sisesta e-posti aadress")]
    [EmailAddress(ErrorMessage = "E-posti aadress ei ole korrektne")]
    [StringLength(256)]
    [Display(Name = "E-post")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sisesta parool")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Parool peab olema vähemalt 8 märki pikk")]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "Parool peab sisaldama vähemalt ühte tähte ja ühte numbrit")]
    [DataType(DataType.Password)]
    [Display(Name = "Parool")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Korda parooli")]
    [Compare(nameof(Password), ErrorMessage = "Paroolid ei ühti")]
    [DataType(DataType.Password)]
    [Display(Name = "Korda parooli")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sisesta eesnimi")]
    [StringLength(100)]
    [Display(Name = "Eesnimi")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sisesta perekonnanimi")]
    [StringLength(100)]
    [Display(Name = "Perekonnanimi")]
    public string LastName { get; set; } = string.Empty;
}

public class RegisterStudentViewModel : RegisterViewModel
{
    [Required(ErrorMessage = "Sisesta kooli nimi")]
    [StringLength(200)]
    [Display(Name = "Kool")]
    public string SchoolName { get; set; } = "Tallinna Tööstushariduskeskus";

    [Required(ErrorMessage = "Sisesta õppekava")]
    [StringLength(200)]
    [Display(Name = "Õppekava")]
    public string StudyProgram { get; set; } = string.Empty;

    [Range(1, 6, ErrorMessage = "Kursus peab olema 1 kuni 6")]
    [Display(Name = "Kursus")]
    public int Course { get; set; } = 1;

    [Phone(ErrorMessage = "Telefoninumber ei ole korrektne")]
    [StringLength(30)]
    [Display(Name = "Telefon")]
    public string? PhoneNumber { get; set; }

    [StringLength(150)]
    [Display(Name = "Asukoht")]
    public string? Location { get; set; }
}

public class RegisterCompanyViewModel : RegisterViewModel
{
    [Required(ErrorMessage = "Sisesta ettevõtte nimi")]
    [StringLength(200)]
    [Display(Name = "Ettevõtte nimi")]
    public string CompanyName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sisesta registrikood")]
    [StringLength(50)]
    [Display(Name = "Registrikood")]
    public string RegistrationCode { get; set; } = string.Empty;

    [StringLength(3000)]
    [Display(Name = "Ettevõtte tutvustus")]
    public string? Description { get; set; }

    [Url(ErrorMessage = "Veebiaadress peab algama http:// või https://")]
    [StringLength(300)]
    [Display(Name = "Veebileht")]
    public string? Website { get; set; }

    [StringLength(300)]
    [Display(Name = "Aadress")]
    public string? Address { get; set; }

    [EmailAddress(ErrorMessage = "E-posti aadress ei ole korrektne")]
    [StringLength(256)]
    [Display(Name = "Kontakt e-post")]
    public string? ContactEmail { get; set; }

    [Phone(ErrorMessage = "Telefoninumber ei ole korrektne")]
    [StringLength(30)]
    [Display(Name = "Kontakt telefon")]
    public string? ContactPhone { get; set; }
}
