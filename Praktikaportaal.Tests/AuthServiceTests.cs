using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Core.Exceptions;
using Praktikaportaal.Core.Interfaces;
using Praktikaportaal.Infrastructure.Persistence;
using Praktikaportaal.Core.Services;
using Praktikaportaal.Infrastructure.Persistence.Repositories;
using Praktikaportaal.Infrastructure.Services;
using Xunit;

namespace Praktikaportaal.Tests;

public class AuthServiceTests
{
    private PraktikaportaalDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PraktikaportaalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new PraktikaportaalDbContext(options);
    }

    [Fact]
    public async Task RegisterStudent_CreatesUserAndStudentProfile()
    {
        using var context = CreateDbContext();
        var passwordHasher = new PasswordHasher();
        var jwtMock = new Mock<IJwtTokenService>();
        jwtMock.Setup(j => j.GenerateToken(It.IsAny<User>(), It.IsAny<Guid?>())).Returns("mock_token");

        var authService = new AuthService(new UserRepository(context), new UnitOfWork(context), passwordHasher, jwtMock.Object);

        var dto = new RegisterStudentDto
        {
            Email = "student@tthk.ee",
            Password = "Password123!",
            FirstName = "Mari",
            LastName = "Tamm",
            SchoolName = "Tallinna Tööstushariduskeskus",
            StudyProgram = "Noorem tarkvaraarendaja",
            Course = 2
        };

        var result = await authService.RegisterStudentAsync(dto);

        Assert.NotNull(result.Token);
        Assert.NotNull(result.User);
        Assert.Equal(UserRole.Student, result.User.Role);
        Assert.NotNull(result.User.ProfileId);

        var userInDb = await context.Users
            .Include(u => u.StudentProfile)
            .FirstOrDefaultAsync(u => u.Email == "student@tthk.ee");

        Assert.NotNull(userInDb);
        Assert.NotNull(userInDb.StudentProfile);
        Assert.Equal("Tallinna Tööstushariduskeskus", userInDb.StudentProfile.SchoolName);
        Assert.True(passwordHasher.VerifyPassword("Password123!", userInDb.PasswordHash));
    }

    [Fact]
    public async Task RegisterStudent_WithExistingEmail_ThrowsConflict()
    {
        using var context = CreateDbContext();
        var passwordHasher = new PasswordHasher();
        var jwtMock = new Mock<IJwtTokenService>();

        var authService = new AuthService(new UserRepository(context), new UnitOfWork(context), passwordHasher, jwtMock.Object);

        var existingUser = new User
        {
            Email = "existing@tthk.ee",
            PasswordHash = passwordHasher.HashPassword("Password123!"),
            FirstName = "Peeter",
            LastName = "Kask",
            Role = UserRole.Student
        };
        await context.Users.AddAsync(existingUser);
        await context.SaveChangesAsync();

        var dto = new RegisterStudentDto
        {
            Email = "existing@tthk.ee",
            Password = "AnotherPassword123!",
            FirstName = "Test",
            LastName = "User",
            SchoolName = "TTHK",
            StudyProgram = "IT",
            Course = 1
        };

        var ex = await Assert.ThrowsAsync<ConflictException>(() => authService.RegisterStudentAsync(dto));
        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsSuccessAndToken()
    {
        using var context = CreateDbContext();
        var passwordHasher = new PasswordHasher();
        var jwtMock = new Mock<IJwtTokenService>();
        jwtMock.Setup(j => j.GenerateToken(It.IsAny<User>(), It.IsAny<Guid?>())).Returns("valid_jwt_token");

        var authService = new AuthService(new UserRepository(context), new UnitOfWork(context), passwordHasher, jwtMock.Object);

        var user = new User
        {
            Email = "company@tech.ee",
            PasswordHash = passwordHasher.HashPassword("SecretPassword123!"),
            FirstName = "Jaan",
            LastName = "Sepp",
            Role = UserRole.Company,
            IsActive = true
        };
        var companyProfile = new CompanyProfile
        {
            UserId = user.Id,
            User = user,
            CompanyName = "Tech OU",
            RegistrationCode = "12345678"
        };
        user.CompanyProfile = companyProfile;

        await context.Users.AddAsync(user);
        await context.CompanyProfiles.AddAsync(companyProfile);
        await context.SaveChangesAsync();

        var loginDto = new LoginDto
        {
            Email = "company@tech.ee",
            Password = "SecretPassword123!"
        };

        var result = await authService.LoginAsync(loginDto);

        Assert.Equal("valid_jwt_token", result.Token);
        Assert.Equal(UserRole.Company, result.User?.Role);
        Assert.Equal(companyProfile.Id, result.User?.ProfileId);
    }
}
