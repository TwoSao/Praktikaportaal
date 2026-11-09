using Microsoft.EntityFrameworkCore;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Core.Exceptions;
using Praktikaportaal.Infrastructure.Persistence;
using Praktikaportaal.Core.Services;
using Praktikaportaal.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Praktikaportaal.Tests;

public class ApplicationServiceTests
{
    private PraktikaportaalDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PraktikaportaalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new PraktikaportaalDbContext(options);
    }

    private static ApplicationService CreateService(PraktikaportaalDbContext context) => new(
        new ApplicationRepository(context),
        new InternshipRepository(context),
        new StudentProfileRepository(context),
        new UnitOfWork(context));

    private async Task<(User studentUser, StudentProfile student, CompanyProfile company, Internship internship)> SeedDataAsync(
        PraktikaportaalDbContext context,
        InternshipStatus internshipStatus = InternshipStatus.Published,
        DateTime? deadline = null)
    {
        var studentUser = new User
        {
            Email = "student@test.ee",
            PasswordHash = "hash",
            FirstName = "Mari",
            LastName = "Tamm",
            Role = UserRole.Student
        };
        var student = new StudentProfile
        {
            UserId = studentUser.Id,
            User = studentUser,
            SchoolName = "TTHK",
            StudyProgram = "IT",
            Course = 2
        };
        studentUser.StudentProfile = student;

        var companyUser = new User
        {
            Email = "company@test.ee",
            PasswordHash = "hash",
            FirstName = "Jaan",
            LastName = "Kask",
            Role = UserRole.Company
        };
        var company = new CompanyProfile
        {
            UserId = companyUser.Id,
            User = companyUser,
            CompanyName = "Code OÜ",
            RegistrationCode = "12345678"
        };
        companyUser.CompanyProfile = company;

        var internship = new Internship
        {
            CompanyProfileId = company.Id,
            CompanyProfile = company,
            Title = ".NET Junior Developer Practice",
            Description = "Join our software development team.",
            Field = "IT",
            Status = internshipStatus,
            ApplicationDeadline = deadline
        };

        await context.Users.AddRangeAsync(studentUser, companyUser);
        await context.StudentProfiles.AddAsync(student);
        await context.CompanyProfiles.AddAsync(company);
        await context.Internships.AddAsync(internship);
        await context.SaveChangesAsync();

        return (studentUser, student, company, internship);
    }

    [Fact]
    public async Task Apply_ToPublishedInternship_Succeeds()
    {
        using var context = CreateDbContext();
        var (_, student, _, internship) = await SeedDataAsync(context);
        var service = CreateService(context);

        var dto = new CreateApplicationDto
        {
            InternshipId = internship.Id,
            CoverLetter = "I am excited to apply!"
        };

        var appDto = await service.ApplyAsync(student.Id, dto);

        Assert.Equal(ApplicationStatus.Submitted, appDto.Status);
        Assert.Equal("Mari Tamm", appDto.StudentName);

        var count = await context.Applications.CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Apply_DuplicateActiveApplication_IsBlocked()
    {
        using var context = CreateDbContext();
        var (_, student, _, internship) = await SeedDataAsync(context);
        var service = CreateService(context);

        var dto = new CreateApplicationDto
        {
            InternshipId = internship.Id,
            CoverLetter = "First application"
        };

        await service.ApplyAsync(student.Id, dto);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.ApplyAsync(student.Id, dto));
        Assert.Contains("already have an active application", ex.Message);
    }

    [Fact]
    public async Task Apply_AfterWithdrawn_Succeeds()
    {
        using var context = CreateDbContext();
        var (_, student, _, internship) = await SeedDataAsync(context);
        var service = CreateService(context);

        var dto = new CreateApplicationDto
        {
            InternshipId = internship.Id,
            CoverLetter = "First attempt"
        };

        var first = await service.ApplyAsync(student.Id, dto);

        // Student withdraws
        await service.WithdrawApplicationAsync(first.Id, student.Id);

        // Reapply after withdrawal must succeed
        var second = await service.ApplyAsync(student.Id, new CreateApplicationDto
        {
            InternshipId = internship.Id,
            CoverLetter = "Reapplying with updated CV"
        });

        Assert.Equal(ApplicationStatus.Submitted, second.Status);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task Apply_WhenDeadlinePassed_ThrowsBusinessRule()
    {
        using var context = CreateDbContext();
        var (_, student, _, internship) = await SeedDataAsync(
            context,
            internshipStatus: InternshipStatus.Published,
            deadline: DateTime.UtcNow.AddDays(-1)); // Deadline yesterday

        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ApplyAsync(student.Id, new CreateApplicationDto
        {
            InternshipId = internship.Id,
            CoverLetter = "Late application"
        }));

        Assert.Contains("deadline", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateApplicationStatus_ByOwningCompany_Succeeds()
    {
        using var context = CreateDbContext();
        var (_, student, company, internship) = await SeedDataAsync(context);
        var service = CreateService(context);

        var applyResult = await service.ApplyAsync(student.Id, new CreateApplicationDto
        {
            InternshipId = internship.Id,
            CoverLetter = "Please hire me"
        });

        await service.UpdateApplicationStatusAsync(
            applyResult.Id,
            company.Id,
            ApplicationStatus.Accepted);

        var appInDb = await context.Applications.FindAsync(applyResult.Id);
        Assert.NotNull(appInDb);
        Assert.Equal(ApplicationStatus.Accepted, appInDb.Status);
    }
}
