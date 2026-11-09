using Microsoft.EntityFrameworkCore;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Core.Interfaces;
using Praktikaportaal.Infrastructure.Persistence;

namespace Praktikaportaal.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly PraktikaportaalDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(
        PraktikaportaalDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AuthResultDto> RegisterStudentAsync(RegisterStudentDto dto, CancellationToken ct = default)
    {
        string normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        bool emailExists = await _context.Users
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail, ct);

        if (emailExists)
            return AuthResultDto.Failed("User with this email already exists.");

        var user = new User
        {
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.HashPassword(dto.Password),
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Role = UserRole.Student,
            IsActive = true
        };

        var studentProfile = new StudentProfile
        {
            UserId = user.Id,
            User = user,
            SchoolName = dto.SchoolName.Trim(),
            StudyProgram = dto.StudyProgram.Trim(),
            Course = dto.Course,
            PhoneNumber = dto.PhoneNumber?.Trim(),
            Location = dto.Location?.Trim()
        };

        user.StudentProfile = studentProfile;

        await _context.Users.AddAsync(user, ct);
        await _context.StudentProfiles.AddAsync(studentProfile, ct);
        await _context.SaveChangesAsync(ct);

        string token = _jwtTokenService.GenerateToken(user, studentProfile.Id);

        var userDto = new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            ProfileId = studentProfile.Id
        };

        return AuthResultDto.Succeeded(token, userDto);
    }

    public async Task<AuthResultDto> RegisterCompanyAsync(RegisterCompanyDto dto, CancellationToken ct = default)
    {
        string normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        bool emailExists = await _context.Users
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail, ct);

        if (emailExists)
            return AuthResultDto.Failed("User with this email already exists.");

        var user = new User
        {
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.HashPassword(dto.Password),
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Role = UserRole.Company,
            IsActive = true
        };

        var companyProfile = new CompanyProfile
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

        user.CompanyProfile = companyProfile;

        await _context.Users.AddAsync(user, ct);
        await _context.CompanyProfiles.AddAsync(companyProfile, ct);
        await _context.SaveChangesAsync(ct);

        string token = _jwtTokenService.GenerateToken(user, companyProfile.Id);

        var userDto = new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            ProfileId = companyProfile.Id
        };

        return AuthResultDto.Succeeded(token, userDto);
    }

    public async Task<AuthResultDto> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        string normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .Include(u => u.StudentProfile)
            .Include(u => u.CompanyProfile)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, ct);

        if (user == null)
            return AuthResultDto.Failed("Invalid email or password.");

        if (!user.IsActive)
            return AuthResultDto.Failed("Account is deactivated. Please contact support.");

        bool isPasswordValid = _passwordHasher.VerifyPassword(dto.Password, user.PasswordHash);
        if (!isPasswordValid)
            return AuthResultDto.Failed("Invalid email or password.");

        Guid? profileId = user.Role switch
        {
            UserRole.Student => user.StudentProfile?.Id,
            UserRole.Company => user.CompanyProfile?.Id,
            _ => null
        };

        string token = _jwtTokenService.GenerateToken(user, profileId);

        var userDto = new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            ProfileId = profileId
        };

        return AuthResultDto.Succeeded(token, userDto);
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _context.Users
            .Include(u => u.StudentProfile)
            .Include(u => u.CompanyProfile)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
            return null;

        Guid? profileId = user.Role switch
        {
            UserRole.Student => user.StudentProfile?.Id,
            UserRole.Company => user.CompanyProfile?.Id,
            _ => null
        };

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            ProfileId = profileId
        };
    }
}

