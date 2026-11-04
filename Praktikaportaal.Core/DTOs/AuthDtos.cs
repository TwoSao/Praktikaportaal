using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Core.DTOs;

public class RegisterStudentDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string SchoolName { get; set; } = string.Empty;
    public string StudyProgram { get; set; } = string.Empty;
    public int Course { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Location { get; set; }
}

public class RegisterCompanyDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string RegistrationCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Website { get; set; }
    public string? Address { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
}

public class LoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public Guid? ProfileId { get; set; }
}

public class AuthResultDto
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? Error { get; set; }
    public UserDto? User { get; set; }

    public static AuthResultDto Succeeded(string token, UserDto user) =>
        new() { Success = true, Token = token, User = user };

    public static AuthResultDto Failed(string error) =>
        new() { Success = false, Error = error };
}
