using Microsoft.EntityFrameworkCore;
using Moq;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Core.Exceptions;
using Praktikaportaal.Core.Interfaces.Repositories;
using Praktikaportaal.Core.Services;
using Praktikaportaal.Infrastructure.Persistence;
using Praktikaportaal.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Praktikaportaal.Tests;

public class StudentServiceTests
{
    private static async Task<(PraktikaportaalDbContext Context, StudentService Service, StudentProfile Student, Skill[] Skills)> SetupAsync()
    {
        var context = new PraktikaportaalDbContext(new DbContextOptionsBuilder<PraktikaportaalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var user = new User { Email = "mari@student.ee", FirstName = "Mari", LastName = "Tamm", Role = UserRole.Student };
        var student = new StudentProfile { User = user, UserId = user.Id, SchoolName = "TTHK", StudyProgram = "IT", Course = 2 };
        var skills = new[] { new Skill { Name = "C#" }, new Skill { Name = "SQL" }, new Skill { Name = "Git" } };

        context.Users.Add(user);
        context.StudentProfiles.Add(student);
        context.Skills.AddRange(skills);
        await context.SaveChangesAsync();

        var service = new StudentService(new StudentProfileRepository(context), new SkillRepository(context), new UnitOfWork(context));
        return (context, service, student, skills);
    }

    private static UpdateStudentProfileDto Update(params StudentSkillInputDto[] skills) => new()
    {
        FirstName = "Mari-Liis",
        LastName = "Tamm",
        SchoolName = "TalTech",
        StudyProgram = "Informatics",
        Course = 3,
        Location = "Tallinn",
        Skills = [.. skills]
    };

    [Fact]
    public async Task UpdateProfile_SavesFieldsAndSkillsWithNames()
    {
        var (_, service, student, skills) = await SetupAsync();

        var result = await service.UpdateProfileAsync(student.Id, Update(
            new StudentSkillInputDto { SkillId = skills[0].Id, Level = SkillLevel.Advanced },
            new StudentSkillInputDto { SkillId = skills[1].Id, Level = SkillLevel.Beginner }));

        Assert.Equal("Mari-Liis", result.FirstName);
        Assert.Equal("TalTech", result.SchoolName);
        Assert.Equal(["C#", "SQL"], result.Skills.Select(s => s.Name));
        Assert.Equal(SkillLevel.Advanced, result.Skills.Single(s => s.Name == "C#").Level);
    }

    [Fact]
    public async Task UpdateProfile_ReplacesSkillList_AndUpdatesLevels()
    {
        var (context, service, student, skills) = await SetupAsync();
        await service.UpdateProfileAsync(student.Id, Update(
            new StudentSkillInputDto { SkillId = skills[0].Id, Level = SkillLevel.Beginner },
            new StudentSkillInputDto { SkillId = skills[1].Id, Level = SkillLevel.Beginner }));

        // Drop SQL, keep C# with a higher level, add Git.
        var result = await service.UpdateProfileAsync(student.Id, Update(
            new StudentSkillInputDto { SkillId = skills[0].Id, Level = SkillLevel.Expert },
            new StudentSkillInputDto { SkillId = skills[2].Id, Level = SkillLevel.Intermediate }));

        Assert.Equal(["C#", "Git"], result.Skills.Select(s => s.Name));
        Assert.Equal(SkillLevel.Expert, result.Skills.Single(s => s.Name == "C#").Level);
        Assert.Equal(2, await context.StudentSkills.CountAsync());
    }

    [Fact]
    public async Task UpdateProfile_DuplicateSkill_ThrowsBusinessRule()
    {
        var (_, service, student, skills) = await SetupAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateProfileAsync(student.Id, Update(
            new StudentSkillInputDto { SkillId = skills[0].Id },
            new StudentSkillInputDto { SkillId = skills[0].Id })));
    }

    [Fact]
    public async Task UpdateProfile_UnknownSkill_ThrowsAndDoesNotSave()
    {
        var students = new Mock<IStudentProfileRepository>();
        var skills = new Mock<ISkillRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var user = new User { FirstName = "Mari", LastName = "Tamm" };
        var student = new StudentProfile { User = user };
        students.Setup(r => r.GetWithSkillsAsync(student.Id, It.IsAny<CancellationToken>())).ReturnsAsync(student);
        skills.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var service = new StudentService(students.Object, skills.Object, unitOfWork.Object);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateProfileAsync(student.Id, Update(new StudentSkillInputDto { SkillId = Guid.NewGuid() })));

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal("Mari", user.FirstName); // profile was not modified before validation failed
    }

    [Fact]
    public async Task GetStudents_ReturnsPagedList()
    {
        var (context, service, _, _) = await SetupAsync();
        for (int i = 0; i < 4; i++)
        {
            var u = new User { Email = $"s{i}@student.ee", FirstName = $"Name{i}", LastName = "Zeta", Role = UserRole.Student };
            context.StudentProfiles.Add(new StudentProfile { User = u, UserId = u.Id, SchoolName = "S", StudyProgram = "P", Course = 1 });
        }
        await context.SaveChangesAsync();

        var page = await service.GetStudentsAsync(new PagingParameters { Page = 2, PageSize = 2 });

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(3, page.TotalPages);
    }

    [Fact]
    public async Task GetProfile_Unknown_ThrowsNotFound()
    {
        var (_, service, _, _) = await SetupAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetProfileAsync(Guid.NewGuid()));
    }
}
