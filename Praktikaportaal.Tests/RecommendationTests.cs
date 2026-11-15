using Microsoft.EntityFrameworkCore;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Core.Exceptions;
using Praktikaportaal.Core.Services;
using Praktikaportaal.Infrastructure.Persistence;
using Praktikaportaal.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Praktikaportaal.Tests;

public class RecommendationCalculatorTests
{
    private static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid(), D = Guid.NewGuid();

    [Fact]
    public void AllRequiredSkillsOwned_Is100() =>
        Assert.Equal(100, RecommendationCalculator.OverlapPercent(new HashSet<Guid> { A, B, C }, [A, B]));

    [Fact]
    public void HalfOwned_Is50() =>
        Assert.Equal(50, RecommendationCalculator.OverlapPercent(new HashSet<Guid> { A }, [A, B]));

    [Fact]
    public void NothingOwned_Is0() =>
        Assert.Equal(0, RecommendationCalculator.OverlapPercent(new HashSet<Guid> { D }, [A, B]));

    [Fact]
    public void OneOfThree_RoundsToOneDecimal() =>
        Assert.Equal(33.3, RecommendationCalculator.OverlapPercent(new HashSet<Guid> { A }, [A, B, C]));

    [Fact]
    public void TwoOfThree_RoundsToOneDecimal() =>
        Assert.Equal(66.7, RecommendationCalculator.OverlapPercent(new HashSet<Guid> { A, B }, [A, B, C]));

    [Fact]
    public void NoRequiredSkills_Is0() =>
        Assert.Equal(0, RecommendationCalculator.OverlapPercent(new HashSet<Guid> { A }, []));

    [Fact]
    public void DuplicateRequiredSkills_AreCountedOnce() =>
        Assert.Equal(50, RecommendationCalculator.OverlapPercent(new HashSet<Guid> { A }, [A, A, B]));

    [Fact]
    public void ExtraStudentSkills_DoNotRaiseOverlapAbove100() =>
        Assert.Equal(100, RecommendationCalculator.OverlapPercent(new HashSet<Guid> { A, B, C, D }, [A]));
}

public class RecommendationServiceTests
{
    private record Setup(
        PraktikaportaalDbContext Context,
        RecommendationService Service,
        StudentProfile Student,
        CompanyProfile Company,
        Dictionary<string, Skill> Skills);

    private static async Task<Setup> CreateAsync(params string[] studentSkills)
    {
        var context = new PraktikaportaalDbContext(new DbContextOptionsBuilder<PraktikaportaalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var skills = new[] { "C#", "SQL", "Git", "Docker", "Figma" }.ToDictionary(n => n, n => new Skill { Name = n });
        context.Skills.AddRange(skills.Values);

        var user = new User { Email = "s@student.ee", FirstName = "Mari", LastName = "Tamm", Role = UserRole.Student };
        var student = new StudentProfile { User = user, UserId = user.Id, SchoolName = "S", StudyProgram = "P", Course = 1 };
        foreach (var name in studentSkills)
            student.StudentSkills.Add(new StudentSkill { Skill = skills[name], StudentProfile = student });

        var companyUser = new User { Email = "c@test.ee", Role = UserRole.Company };
        var company = new CompanyProfile { User = companyUser, UserId = companyUser.Id, CompanyName = "Test OÜ", RegistrationCode = "1" };

        context.StudentProfiles.Add(student);
        context.CompanyProfiles.Add(company);
        await context.SaveChangesAsync();

        var service = new RecommendationService(
            new StudentProfileRepository(context), new InternshipRepository(context), new ApplicationRepository(context));
        return new Setup(context, service, student, company, skills);
    }

    private static Internship AddInternship(
        Setup s, string title, string[] required, InternshipStatus status = InternshipStatus.Published, DateTime? deadline = null)
    {
        var internship = new Internship
        {
            CompanyProfile = s.Company,
            CompanyProfileId = s.Company.Id,
            Title = title,
            Description = "d",
            Field = "IT",
            Status = status,
            ApplicationDeadline = deadline
        };
        foreach (var name in required)
            internship.InternshipSkills.Add(new InternshipSkill { Internship = internship, Skill = s.Skills[name] });
        s.Context.Internships.Add(internship);
        return internship;
    }

    private static CurrentUser AsStudent(StudentProfile st) => new(st.UserId, UserRole.Student, st.Id);

    [Fact]
    public async Task Recommendations_AreSortedByOverlapDescending_WithMatchedAndMissingSkills()
    {
        var s = await CreateAsync("C#", "SQL");
        AddInternship(s, "Half", ["C#", "Docker"]);               // 50%
        AddInternship(s, "Full", ["C#", "SQL"]);                  // 100%
        AddInternship(s, "Third", ["C#", "Git", "Docker"]);       // 33.3%
        await s.Context.SaveChangesAsync();

        var result = await s.Service.GetRecommendationsAsync(s.Student.Id, new RecommendationQueryParameters(), AsStudent(s.Student));

        Assert.Equal(["Full", "Half", "Third"], result.Items.Select(r => r.Internship.Title));
        Assert.Equal([100, 50, 33.3], result.Items.Select(r => r.OverlapPercent));
        Assert.Equal(["C#"], result.Items[1].MatchedSkills);
        Assert.Equal(["Docker"], result.Items[1].MissingSkills);
    }

    [Fact]
    public async Task Recommendations_ExcludeZeroOverlap_NoRequirements_Drafts_Expired_AndAlreadyApplied()
    {
        var s = await CreateAsync("C#");
        AddInternship(s, "Match", ["C#"]);
        AddInternship(s, "No overlap", ["Figma"]);
        AddInternship(s, "No requirements", []);
        AddInternship(s, "Draft", ["C#"], InternshipStatus.Draft);
        AddInternship(s, "Closed", ["C#"], InternshipStatus.Closed);
        AddInternship(s, "Expired", ["C#"], deadline: DateTime.UtcNow.AddDays(-1));
        var applied = AddInternship(s, "Applied", ["C#"]);
        s.Context.Applications.Add(new Application { Internship = applied, StudentProfile = s.Student, Status = ApplicationStatus.Submitted });
        var withdrawn = AddInternship(s, "Withdrawn earlier", ["C#"]);
        s.Context.Applications.Add(new Application { Internship = withdrawn, StudentProfile = s.Student, Status = ApplicationStatus.Withdrawn });
        await s.Context.SaveChangesAsync();

        var result = await s.Service.GetRecommendationsAsync(s.Student.Id, new RecommendationQueryParameters(), AsStudent(s.Student));

        Assert.Equal(["Match", "Withdrawn earlier"], result.Items.Select(r => r.Internship.Title).Order());
    }

    [Fact]
    public async Task Recommendations_FilterBySinceAndMinOverlap_AndPaginate()
    {
        var s = await CreateAsync("C#", "SQL", "Git");
        AddInternship(s, "Old", ["C#"]);
        await s.Context.SaveChangesAsync();

        var cutoff = DateTime.UtcNow;       // the Worker's "last run"
        await Task.Delay(30);               // CreatedAt is stamped on save; make sure it is after the cutoff
        AddInternship(s, "New full", ["C#", "SQL"]);
        AddInternship(s, "New half", ["C#", "Docker"]);
        await s.Context.SaveChangesAsync();

        var since = await s.Service.GetRecommendationsAsync(
            s.Student.Id, new RecommendationQueryParameters { Since = cutoff }, AsStudent(s.Student));
        var strong = await s.Service.GetRecommendationsAsync(
            s.Student.Id, new RecommendationQueryParameters { MinOverlap = 75 }, AsStudent(s.Student));
        var page2 = await s.Service.GetRecommendationsAsync(
            s.Student.Id, new RecommendationQueryParameters { Page = 2, PageSize = 2 }, AsStudent(s.Student));

        Assert.Equal(["New full", "New half"], since.Items.Select(r => r.Internship.Title));
        Assert.Equal(["New full", "Old"], strong.Items.Select(r => r.Internship.Title).Order());
        Assert.Equal(3, page2.TotalCount);
        Assert.Single(page2.Items);
    }

    [Fact]
    public async Task Recommendations_OtherStudent_IsForbidden_ButServiceAndAdminAreAllowed()
    {
        var s = await CreateAsync("C#");
        AddInternship(s, "Match", ["C#"]);
        await s.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.GetRecommendationsAsync(
            s.Student.Id, new RecommendationQueryParameters(), new CurrentUser(Guid.NewGuid(), UserRole.Student, Guid.NewGuid())));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.GetRecommendationsAsync(
            s.Student.Id, new RecommendationQueryParameters(), new CurrentUser(Guid.NewGuid(), UserRole.Company, s.Company.Id)));

        var byService = await s.Service.GetRecommendationsAsync(
            s.Student.Id, new RecommendationQueryParameters(), new CurrentUser(Guid.NewGuid(), UserRole.Service, null));
        var byAdmin = await s.Service.GetRecommendationsAsync(
            s.Student.Id, new RecommendationQueryParameters(), new CurrentUser(Guid.NewGuid(), UserRole.Admin, null));
        Assert.Single(byService.Items);
        Assert.Single(byAdmin.Items);
    }

    [Fact]
    public async Task Recommendations_UnknownStudent_ThrowsNotFound()
    {
        var s = await CreateAsync();
        var id = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.GetRecommendationsAsync(
            id, new RecommendationQueryParameters(), new CurrentUser(Guid.NewGuid(), UserRole.Admin, null)));
    }
}
