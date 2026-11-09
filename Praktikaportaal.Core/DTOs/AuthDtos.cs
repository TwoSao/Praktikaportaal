using System.ComponentModel.DataAnnotations;
using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Core.DTOs;

/// <summary>Common fields for every registration request.</summary>
public abstract class RegisterUserDto
{
    /// <summary>Login email (unique).</summary>
    /// <example>new.student@student.ee</example>
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Password, 8–100 characters, must contain a letter and a digit.</summary>
    /// <example>Student123!</example>
    [Required, StringLength(100, MinimumLength = 8)]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "Password must contain at least one letter and one digit.")]
    public string Password { get; set; } = string.Empty;

    /// <example>Mari</example>
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    /// <example>Tamm</example>
    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;
}

/// <summary>Student registration.</summary>
public class RegisterStudentDto : RegisterUserDto
{
    /// <example>Tallinna Tööstushariduskeskus</example>
    [Required, MaxLength(200)]
    public string SchoolName { get; set; } = string.Empty;

    /// <example>Software Development</example>
    [Required, MaxLength(200)]
    public string StudyProgram { get; set; } = string.Empty;

    /// <summary>Current year of study.</summary>
    /// <example>2</example>
    [Range(1, 6)]
    public int Course { get; set; }

    /// <example>+372 5555 1234</example>
    [Phone, MaxLength(30)]
    public string? PhoneNumber { get; set; }

    /// <example>Tallinn</example>
    [MaxLength(150)]
    public string? Location { get; set; }
}

/// <summary>Company registration (the user becomes the company's HR contact).</summary>
public class RegisterCompanyDto : RegisterUserDto
{
    /// <example>Nordic Software OÜ</example>
    [Required, MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>Business registry code.</summary>
    /// <example>14500099</example>
    [Required, MaxLength(50)]
    public string RegistrationCode { get; set; } = string.Empty;

    /// <example>Custom business software and cloud solutions.</example>
    [MaxLength(3000)]
    public string? Description { get; set; }

    /// <example>https://nordicsoftware.example</example>
    [Url, MaxLength(300)]
    public string? Website { get; set; }

    /// <example>Tallinn, Ülemiste City</example>
    [MaxLength(300)]
    public string? Address { get; set; }

    /// <example>hr@nordicsoftware.ee</example>
    [EmailAddress, MaxLength(256)]
    public string? ContactEmail { get; set; }

    /// <example>+372 600 0000</example>
    [Phone, MaxLength(30)]
    public string? ContactPhone { get; set; }
}

public class LoginDto
{
    /// <example>mari.tamm@student.ee</example>
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <example>Student123!</example>
    [Required]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Public user data (never contains the password hash).</summary>
public class UserDto
{
    public Guid Id { get; set; }

    /// <example>mari.tamm@student.ee</example>
    public string Email { get; set; } = string.Empty;

    /// <example>Mari</example>
    public string FirstName { get; set; } = string.Empty;

    /// <example>Tamm</example>
    public string LastName { get; set; } = string.Empty;

    /// <example>Student</example>
    public UserRole Role { get; set; }

    /// <summary>StudentProfile or CompanyProfile id; null for Admin and Service.</summary>
    public Guid? ProfileId { get; set; }
}

/// <summary>Successful login/registration result: JWT access token and the user.</summary>
public class AuthResultDto
{
    /// <summary>JWT to send as "Authorization: Bearer {token}".</summary>
    /// <example>eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...</example>
    public string Token { get; set; } = string.Empty;

    public UserDto User { get; set; } = new();
}
