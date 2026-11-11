using Microsoft.EntityFrameworkCore;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Core.Services;
using Praktikaportaal.Infrastructure.Persistence;
using Praktikaportaal.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Praktikaportaal.Tests;

/// <summary>Search, filter, sort and pagination of the internship list (InMemory database).</summary>
public class InternshipSearchTests
{
    private static async Task<InternshipService> CreateServiceWithDataAsync()
    {
        var context = new PraktikaportaalDbContext(new DbContextOptionsBuilder<PraktikaportaalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var company = new CompanyProfile { CompanyName = "Test OÜ", RegistrationCode = "1", User = new User { Email = "c@test.ee" } };

        Internship Make(string title, string field, string location, int deadlineInDays, InternshipStatus status = InternshipStatus.Published) => new()
        {
            CompanyProfile = company,
            Title = title,
            Description = "Description",
            Field = field,
            Location = location,
            ApplicationDeadline = DateTime.UtcNow.AddDays(deadlineInDays),
            Status = status
        };

        context.Internships.AddRange(
            Make("Backend Developer", "IT", "Tallinn", 30),
            Make("Frontend Developer", "IT", "Tartu", 10),
            Make("QA Engineer", "IT", "Tallinn", 20),
            Make("Marketing Assistant", "Marketing", "Tallinn", 5),
            Make("Designer", "Design", "Pärnu", 15),
            Make("Hidden Draft", "IT", "Tallinn", 1, InternshipStatus.Draft));
        await context.SaveChangesAsync();

        return new InternshipService(new InternshipRepository(context), new SkillRepository(context), new UnitOfWork(context));
    }

    [Fact]
    public async Task Search_FiltersByFieldAndLocation_AndExcludesDrafts()
    {
        var service = await CreateServiceWithDataAsync();

        var result = await service.SearchInternshipsAsync(new InternshipQueryParameters { Field = "it", Location = "tallinn" });

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, i => Assert.Equal("IT", i.Field));
        Assert.DoesNotContain(result.Items, i => i.Title == "Hidden Draft");
    }

    [Fact]
    public async Task Search_Paginates_AndReturnsTotalCount()
    {
        var service = await CreateServiceWithDataAsync();

        var page1 = await service.SearchInternshipsAsync(new InternshipQueryParameters { Page = 1, PageSize = 2, Sort = InternshipSort.Title });
        var page3 = await service.SearchInternshipsAsync(new InternshipQueryParameters { Page = 3, PageSize = 2, Sort = InternshipSort.Title });

        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(3, page1.TotalPages);
        Assert.Equal(["Backend Developer", "Designer"], page1.Items.Select(i => i.Title));
        Assert.Single(page3.Items);
        Assert.False(page3.HasNextPage);
    }

    [Fact]
    public async Task Search_SortByDeadline_ClosestFirst()
    {
        var service = await CreateServiceWithDataAsync();

        var result = await service.SearchInternshipsAsync(new InternshipQueryParameters { Sort = InternshipSort.Deadline });

        Assert.Equal("Marketing Assistant", result.Items[0].Title);
        Assert.Equal("Backend Developer", result.Items[^1].Title);
    }

    [Fact]
    public async Task Search_FreeText_MatchesTitleOrCompany()
    {
        var service = await CreateServiceWithDataAsync();

        var byTitle = await service.SearchInternshipsAsync(new InternshipQueryParameters { Search = "developer" });
        var byCompany = await service.SearchInternshipsAsync(new InternshipQueryParameters { Search = "test oü" });

        Assert.Equal(2, byTitle.TotalCount);
        Assert.Equal(5, byCompany.TotalCount);
    }
}
