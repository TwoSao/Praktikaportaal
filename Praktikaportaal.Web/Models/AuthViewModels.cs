using System.ComponentModel.DataAnnotations;

namespace Praktikaportaal.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "E-mail on kohustuslik")]
    [EmailAddress(ErrorMessage = "Vale e-posti formaat")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parool on kohustuslik")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class RegisterStudentViewModel
{
    [Required(ErrorMessage = "E-mail on kohustuslik")]
    [EmailAddress(ErrorMessage = "Vale e-posti formaat")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parool on kohustuslik")]
    [MinLength(6, ErrorMessage = "Parool peab olema vähemalt 6 tähemärki")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kinnita parool")]
    [Compare("Password", ErrorMessage = "Paroolid ei kattu")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Eesnimi on kohustuslik")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Perekonnanimi on kohustuslik")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Õppeasutus on kohustuslik")]
    public string SchoolName { get; set; } = "Tallinna Tööstushariduskeskus";

    [Required(ErrorMessage = "Eriala on kohustuslik")]
    public string StudyProgram { get; set; } = "Noorem tarkvaraarendaja";

    [Required(ErrorMessage = "Kursus on kohustuslik")]
    [Range(1, 6, ErrorMessage = "Kursus peab olema vahemikus 1–6")]
    public int Course { get; set; } = 2;

    public string? PhoneNumber { get; set; }
    public string? Location { get; set; }
}

public class RegisterCompanyViewModel
{
    [Required(ErrorMessage = "E-mail on kohustuslik")]
    [EmailAddress(ErrorMessage = "Vale e-posti formaat")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parool on kohustuslik")]
    [MinLength(6, ErrorMessage = "Parool peab olema vähemalt 6 tähemärki")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kinnita parool")]
    [Compare("Password", ErrorMessage = "Paroolid ei kattu")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kontaktisiku eesnimi on kohustuslik")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kontaktisiku perekonnanimi on kohustuslik")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ettevõtte nimi on kohustuslik")]
    public string CompanyName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Registrikood on kohustuslik")]
    public string RegistrationCode { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? Website { get; set; }
    public string? Address { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
}

