using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Core.Exceptions;
using Praktikaportaal.Core.Interfaces;
using Praktikaportaal.Core.Interfaces.Repositories;

namespace Praktikaportaal.Core.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService)
    {
        _users = users;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AuthResultDto> RegisterStudentAsync(RegisterStudentDto dto, CancellationToken ct = default)
    {
        string normalizedEmail = NormalizeEmail(dto.Email);

        if (await _users.EmailExistsAsync(normalizedEmail, ct))
            throw new ConflictException("User with this email already exists.");

        var user = CreateUser(normalizedEmail, dto.Password, dto.FirstName, dto.LastName, UserRole.Student);

        user.StudentProfile = new StudentProfile
        {
            UserId = user.Id,
            User = user,
            SchoolName = dto.SchoolName.Trim(),
            StudyProgram = dto.StudyProgram.Trim(),
            Course = dto.Course,
            PhoneNumber = dto.PhoneNumber?.Trim(),
            Location = dto.Location?.Trim()
        };

        await _users.AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Succeeded(user, user.StudentProfile.Id);
    }

    public async Task<AuthResultDto> RegisterCompanyAsync(RegisterCompanyDto dto, CancellationToken ct = default)
    {
        string normalizedEmail = NormalizeEmail(dto.Email);

        if (await _users.EmailExistsAsync(normalizedEmail, ct))
            throw new ConflictException("User with this email already exists.");

        var user = CreateUser(normalizedEmail, dto.Password, dto.FirstName, dto.LastName, UserRole.Company);

        user.CompanyProfile = new CompanyProfile
        {
            UserId = user.Id,
            User = user,
            CompanyName = dto.CompanyName.Trim(),
            RegistrationCode = dto.RegistrationCode.Trim(),
            Description = dto.Description?.Trim(),
            Website = dto.Website?.Trim(),
            Address = dto.Address?.Trim(),
            ContactEmail = dto.ContactEmail?.Trim() ?? normalizedEmail,
            ContactPhone = dto.ContactPhone?.Trim()
        };

        await _users.AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Succeeded(user, user.CompanyProfile.Id);
    }

    public async Task<AuthResultDto> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(NormalizeEmail(dto.Email), ct);

        // Same message for unknown email and wrong password (no account enumeration).
        if (user == null || !_passwordHasher.VerifyPassword(dto.Password, user.PasswordHash))
            throw new UnauthorizedException();

        if (!user.IsActive)
            throw new UnauthorizedException("Account is deactivated. Please contact support.");

        return Succeeded(user, GetProfileId(user));
    }

    public async Task<UserDto> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User", userId);
        return MapToDto(user, GetProfileId(user));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private User CreateUser(string email, string password, string firstName, string lastName, UserRole role) => new()
    {
        Email = email,
        PasswordHash = _passwordHasher.HashPassword(password),
        FirstName = firstName.Trim(),
        LastName = lastName.Trim(),
        Role = role,
        IsActive = true
    };

    private static Guid? GetProfileId(User user) => user.Role switch
    {
        UserRole.Student => user.StudentProfile?.Id,
        UserRole.Company => user.CompanyProfile?.Id,
        _ => null
    };

    private AuthResultDto Succeeded(User user, Guid? profileId) =>
        new() { Token = _jwtTokenService.GenerateToken(user, profileId), User = MapToDto(user, profileId) };

    private static UserDto MapToDto(User user, Guid? profileId) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Role = user.Role,
        ProfileId = profileId
    };
}
