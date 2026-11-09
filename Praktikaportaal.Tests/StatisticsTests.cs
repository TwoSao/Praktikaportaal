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

public class PeriodBucketsTests
{
    [Theory]
    [InlineData("2026-11-10", "2026-11-09")]   // Tuesday  -> Monday
    [InlineData("2026-11-09", "2026-11-09")]   // Monday   -> itself
    [InlineData("2026-11-15", "2026-11-09")]   // Sunday   -> previous Monday
    [InlineData("2026-11-16", "2026-11-16")]
    public void Week_StartsOnMonday(string date, string expectedMonday) =>
        Assert.Equal(DateTime.Parse(expectedMonday), PeriodBuckets.Start(DateTime.Parse(date), StatisticsPeriod.Week));

    [Fact]
    public void Month_StartsOnFirstDay() =>
        Assert.Equal(new DateTime(2026, 11, 1), PeriodBuckets.Start(new DateTime(2026, 11, 28, 15, 30, 0), StatisticsPeriod.Month));

    [Fact]
    public void Day_DropsTime() =>
        Assert.Equal(new DateTime(2026, 11, 28), PeriodBuckets.Start(new DateTime(2026, 11, 28, 23, 59, 0), StatisticsPeriod.Day));

    [Fact]
    public void Count_FillsEmptyBucketsBetweenFirstAndLast()
    {
        var dates = new[] { new DateTime(2026, 9, 5), new DateTime(2026, 9, 20), new DateTime(2026, 11, 2) };

        var result = PeriodBuckets.Count(dates, StatisticsPeriod.Month);

        Assert.Equal([new DateTime(2026, 9, 1), new DateTime(2026, 10, 1), new DateTime(2026, 11, 1)], result.Select(r => r.PeriodStart));
        Assert.Equal([2, 0, 1], result.Select(r => r.Count));
    }

    [Fact]
    public void Count_ExtendsToRequestedRange()
    {
        var result = PeriodBuckets.Count([new DateTime(2026, 10, 15)], StatisticsPeriod.Month,
            rangeStart: new DateTime(2026, 9, 10), rangeEnd: new DateTime(2026, 11, 20));

        Assert.Equal([0, 1, 0], result.Select(r => r.Count));
    }

    [Fact]
    public void Count_NoDataAndNoRange_IsEmpty() =>
        Assert.Empty(PeriodBuckets.Count([], StatisticsPeriod.Week));
}

public class StatisticsServiceTests
{
    private static async Task<(StatisticsService Service, PraktikaportaalDbContext Context)> CreateAsync()
    {
        var context = new PraktikaportaalDbContext(new DbContextOptionsBuilder<PraktikaportaalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var companyUser = new User { Email = "c@test.ee", Role = UserRole.Company };
        var company = new CompanyProfile { User = companyUser, UserId = companyUser.Id, CompanyName = "Acme OÜ", RegistrationCode = "1" };

        Internship Make(string title, string field, bool deleted = false) => new()
        {
            CompanyProfile = company, Title = title, Description = "d", Field = field, Status = InternshipStatus.Published, IsDeleted = deleted
        };
        var backend = Make("Backend", "IT");
        var frontend = Make("Frontend", "IT");
        var marketing = Make("Marketing", "Marketing");
        var removed = Make("Removed listing", "Design", deleted: true);

        StudentProfile Student(string name)
        {
            var u = new User { Email = name + "@student.ee", FirstName = name, LastName = "X", Role = UserRole.Student };
            return new StudentProfile { User = u, UserId = u.Id, SchoolName = "S", StudyProgram = "P", Course = 1 };
        }
        var students = new[] { Student("a"), Student("b"), Student("c"), Student("d") };

        context.Internships.AddRange(backend, frontend, marketing, removed);
        context.StudentProfiles.AddRange(students);

        Application App(StudentProfile st, Internship i, ApplicationStatus status, DateTime at) =>
            new() { StudentProfile = st, Internship = i, Status = status, AppliedAt = at };

        context.Applications.AddRange(
            App(students[0], backend, ApplicationStatus.Submitted, new DateTime(2026, 10, 5, 10, 0, 0)),
            App(students[1], backend, ApplicationStatus.Interview, new DateTime(2026, 10, 20, 12, 0, 0)),
            App(students[2], backend, ApplicationStatus.Rejected, new DateTime(2026, 11, 3, 9, 0, 0)),
            App(students[0], frontend, ApplicationStatus.Accepted, new DateTime(2026, 11, 10, 18, 30, 0)),
            App(students[1], marketing, ApplicationStatus.Withdrawn, new DateTime(2026, 11, 10, 8, 0, 0)),
            App(students[3], removed, ApplicationStatus.Submitted, new DateTime(2026, 11, 12, 8, 0, 0)));
        await context.SaveChangesAsync();

        // The context stamps CreatedAt only; AppliedAt is ours.
        return (new StatisticsService(new StatisticsRepository(context)), context);
    }

    [Fact]
    public async Task Statistics_TotalsAndByStatus_ListEveryStatus()
    {
        var (service, _) = await CreateAsync();

        var stats = await service.GetApplicationStatisticsAsync(new StatisticsQueryParameters());

        Assert.Equal(6, stats.TotalApplications);
        Assert.Equal(Enum.GetValues<ApplicationStatus>(), stats.ByStatus.Select(s => s.Status));
        Assert.Equal(2, stats.ByStatus.Single(s => s.Status == ApplicationStatus.Submitted).Count);
        Assert.Equal(0, stats.ByStatus.Single(s => s.Status == ApplicationStatus.Viewed).Count); // no data -> still listed with 0
        Assert.Equal(6, stats.ByStatus.Sum(s => s.Count));
    }

    [Fact]
    public async Task Statistics_ByInternshipAndField_SortedDescending_AndKeepDeletedListings()
    {
        var (service, _) = await CreateAsync();

        var stats = await service.GetApplicationStatisticsAsync(new StatisticsQueryParameters { Top = 2 });

        Assert.Equal(2, stats.TopInternships.Count);
        Assert.Equal("Backend", stats.TopInternships[0].Title);
        Assert.Equal(3, stats.TopInternships[0].Count);
        Assert.Equal("Acme OÜ", stats.TopInternships[0].CompanyName);

        Assert.Equal(["IT", "Design", "Marketing"], stats.ByField.Select(f => f.Field).Take(3));
        Assert.Equal(4, stats.ByField.Single(f => f.Field == "IT").Count);
        Assert.Equal(1, stats.ByField.Single(f => f.Field == "Design").Count); // soft-deleted internship still counted
    }

    [Fact]
    public async Task Statistics_ByPeriod_Monthly_FillsGaps()
    {
        var (service, _) = await CreateAsync();

        var stats = await service.GetApplicationStatisticsAsync(new StatisticsQueryParameters { Period = StatisticsPeriod.Month });

        Assert.Equal([new DateTime(2026, 10, 1), new DateTime(2026, 11, 1)], stats.ByPeriod.Select(p => p.PeriodStart));
        Assert.Equal([2, 4], stats.ByPeriod.Select(p => p.Count));
    }

    [Fact]
    public async Task Statistics_DateRange_ToIncludesWholeDay_AndFiltersEverything()
    {
        var (service, _) = await CreateAsync();

        // 10 Nov: two applications (08:00 and 18:30) -> a bare date must include the evening one.
        var stats = await service.GetApplicationStatisticsAsync(new StatisticsQueryParameters
        {
            From = new DateTime(2026, 11, 10),
            To = new DateTime(2026, 11, 10),
            Period = StatisticsPeriod.Day
        });

        Assert.Equal(2, stats.TotalApplications);
        Assert.Equal(2, stats.ByStatus.Single(s => s.Status == ApplicationStatus.Accepted).Count
                       + stats.ByStatus.Single(s => s.Status == ApplicationStatus.Withdrawn).Count);
        Assert.Single(stats.ByPeriod);
        Assert.Equal(2, stats.ByPeriod[0].Count);
    }

    [Fact]
    public async Task Statistics_RangeWithNoApplications_ReturnsZerosAndEmptyLists()
    {
        var (service, _) = await CreateAsync();

        var stats = await service.GetApplicationStatisticsAsync(new StatisticsQueryParameters
        {
            From = new DateTime(2025, 1, 1), To = new DateTime(2025, 1, 31), Period = StatisticsPeriod.Week
        });

        Assert.Equal(0, stats.TotalApplications);
        Assert.All(stats.ByStatus, s => Assert.Equal(0, s.Count));
        Assert.Empty(stats.TopInternships);
        Assert.Empty(stats.ByField);
        Assert.All(stats.ByPeriod, p => Assert.Equal(0, p.Count)); // zero-filled weeks of January
        Assert.Equal(5, stats.ByPeriod.Count);
    }

    [Fact]
    public async Task Statistics_Overview_CountsPortalTotals_IgnoringDateFilter()
    {
        var (service, _) = await CreateAsync();

        var stats = await service.GetApplicationStatisticsAsync(new StatisticsQueryParameters
        {
            From = new DateTime(2025, 1, 1), To = new DateTime(2025, 1, 2)
        });

        Assert.Equal(4, stats.Overview.Students);
        Assert.Equal(1, stats.Overview.Companies);
        Assert.Equal(3, stats.Overview.PublishedInternships); // the soft-deleted one is not counted
    }

    [Fact]
    public async Task Statistics_InvalidRanges_ThrowBusinessRule()
    {
        var (service, _) = await CreateAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetApplicationStatisticsAsync(
            new StatisticsQueryParameters { From = new DateTime(2026, 11, 10), To = new DateTime(2026, 11, 1) }));

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetApplicationStatisticsAsync(
            new StatisticsQueryParameters { From = new DateTime(2020, 1, 1), To = new DateTime(2026, 1, 1), Period = StatisticsPeriod.Day }));
    }

    [Fact]
    public async Task Statistics_PassesRangeToRepository_ExclusiveEndOfDay()
    {
        var repo = new Mock<IStatisticsRepository>();
        repo.Setup(r => r.CountByStatusAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        repo.Setup(r => r.GetTopInternshipsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        repo.Setup(r => r.CountByFieldAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        repo.Setup(r => r.GetApplicationDatesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        repo.Setup(r => r.GetOverviewAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new PortalOverviewDto());

        await new StatisticsService(repo.Object).GetApplicationStatisticsAsync(new StatisticsQueryParameters
        {
            From = new DateTime(2026, 11, 1), To = new DateTime(2026, 11, 30), Top = 7
        });

        repo.Verify(r => r.CountByStatusAsync(new DateTime(2026, 11, 1), new DateTime(2026, 12, 1), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.GetTopInternshipsAsync(new DateTime(2026, 11, 1), new DateTime(2026, 12, 1), 7, It.IsAny<CancellationToken>()), Times.Once);
    }
}
