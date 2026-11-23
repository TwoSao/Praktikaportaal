using System.Net;
using Moq;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Web.Models;
using Praktikaportaal.Web.Services;

namespace Praktikaportaal.Web.Tests;

public class AdminPagesTests : IClassFixture<WebFactory>
{
    private readonly WebFactory _factory;

    public AdminPagesTests(WebFactory factory)
    {
        _factory = factory;
        _factory.Api.Reset();
        _factory.Api.Setup(a => a.GetNotificationsAsync(It.IsAny<NotificationQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationListDto());
    }

    private async Task<HttpClient> AdminAsync()
    {
        var client = _factory.CreateBrowser();
        await _factory.LoginAsync(client, UserRole.Admin);
        return client;
    }

    private static ApplicationStatisticsDto Stats() => new()
    {
        TotalApplications = 20,
        ByStatus = Enum.GetValues<ApplicationStatus>().Select((s, i) => new StatusCountDto { Status = s, Count = i == 0 ? 8 : i == 3 ? 4 : 0 }).ToList(),
        TopInternships = [new InternshipCountDto { InternshipId = Guid.NewGuid(), Title = "Top Internship", CompanyName = "Acme OÜ", Count = 5 }],
        ByField = [new FieldCountDto { Field = "IT", Count = 12 }, new FieldCountDto { Field = "Design", Count = 6 }],
        Period = StatisticsPeriod.Week,
        ByPeriod =
        [
            new PeriodCountDto { PeriodStart = new DateTime(2026, 10, 26), Count = 5 },
            new PeriodCountDto { PeriodStart = new DateTime(2026, 11, 2), Count = 10 },
            new PeriodCountDto { PeriodStart = new DateTime(2026, 11, 9), Count = 0 }
        ],
        Overview = new PortalOverviewDto { Students = 8, Companies = 5, PublishedInternships = 22 }
    };

    [Theory]
    [InlineData(UserRole.Student)]
    [InlineData(UserRole.Company)]
    public async Task OtherRoles_AreDenied(UserRole role)
    {
        var client = _factory.CreateBrowser();
        await _factory.LoginAsync(client, role);

        var response = await client.GetAsync("/Admin/Statistics");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", WebFactory.LocationOf(response));
        _factory.Api.Verify(a => a.GetStatisticsAsync(It.IsAny<StatisticsQueryParameters>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Anonymous_IsSentToLogin()
    {
        var response = await _factory.CreateBrowser().GetAsync("/Admin/Statistics");

        Assert.StartsWith("/Account/Login", WebFactory.LocationOf(response));
    }

    [Fact]
    public async Task Page_ShowsTotals_StatusBars_Fields_TopInternships_AndTimeline()
    {
        _factory.Api.Setup(a => a.GetStatisticsAsync(It.IsAny<StatisticsQueryParameters>(), It.IsAny<CancellationToken>())).ReturnsAsync(Stats());
        var client = await AdminAsync();

        var html = await WebFactory.BodyAsync(await client.GetAsync("/Admin/Statistics"));

        Assert.Contains("id=\"statTotal\">20<", html);
        Assert.Contains("Esitatud", html);                         // every status is listed, even with 0
        Assert.Contains("Tagasi võetud", html);
        Assert.Contains("width: 100%", html);                      // the largest status bar fills the row
        Assert.Contains("width: 50%", html);                       // 4 of max 8
        Assert.Contains("Top Internship", html);
        Assert.Contains("Acme OÜ", html);
        Assert.Contains("aria-label=\"IT: 12\"", html);
        Assert.Contains("timeline-col", html);
        Assert.Contains(">22<", html);                             // published internships overview
    }

    [Fact]
    public async Task Parameters_ArePassedToTheApi_WeeklyIsTheDefault()
    {
        StatisticsQueryParameters? sent = null;
        _factory.Api.Setup(a => a.GetStatisticsAsync(It.IsAny<StatisticsQueryParameters>(), It.IsAny<CancellationToken>()))
            .Callback<StatisticsQueryParameters, CancellationToken>((q, _) => sent = q)
            .ReturnsAsync(Stats());
        var client = await AdminAsync();

        await client.GetAsync("/Admin/Statistics");
        Assert.Equal(StatisticsPeriod.Week, sent!.Period);
        Assert.Null(sent.From);

        await client.GetAsync("/Admin/Statistics?from=2026-10-01&to=2026-11-30&period=Month&top=5");
        Assert.Equal(new DateTime(2026, 10, 1), sent.From);
        Assert.Equal(new DateTime(2026, 11, 30), sent.To);
        Assert.Equal(StatisticsPeriod.Month, sent.Period);
        Assert.Equal(5, sent.Top);

        await client.GetAsync("/Admin/Statistics?top=9999");
        Assert.Equal(50, sent.Top);                                  // clamped, not an API error
    }

    [Fact]
    public async Task ReversedRange_ShowsAMessage_AndIgnoresTheRange()
    {
        StatisticsQueryParameters? sent = null;
        _factory.Api.Setup(a => a.GetStatisticsAsync(It.IsAny<StatisticsQueryParameters>(), It.IsAny<CancellationToken>()))
            .Callback<StatisticsQueryParameters, CancellationToken>((q, _) => sent = q)
            .ReturnsAsync(Stats());
        var client = await AdminAsync();

        var html = await WebFactory.BodyAsync(await client.GetAsync("/Admin/Statistics?from=2026-11-10&to=2026-11-01"));

        Assert.Contains("Lõppkuupäev ei tohi olla enne alguskuupäeva", html);
        Assert.Null(sent!.From);
        Assert.Null(sent.To);
    }

    [Fact]
    public async Task ApiRejectsTheRange_TheUserSeesTheReason_AndStillGetsData()
    {
        int calls = 0;
        _factory.Api.Setup(a => a.GetStatisticsAsync(It.IsAny<StatisticsQueryParameters>(), It.IsAny<CancellationToken>()))
            .Returns<StatisticsQueryParameters, CancellationToken>((q, _) =>
                ++calls == 1
                    ? throw new ApiException(HttpStatusCode.BadRequest, "Business rule violation", "The range is too long for Day buckets (max 400).")
                    : Task.FromResult(Stats()));
        var client = await AdminAsync();

        var response = await client.GetAsync("/Admin/Statistics?from=2020-01-01&period=Day");

        var html = await WebFactory.BodyAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("liiga pikk", html);
        Assert.Contains("id=\"statTotal\">20<", html);
    }

    [Fact]
    public async Task NoApplications_ShowZeros_AndEmptyMessages_NoDivisionByZero()
    {
        _factory.Api.Setup(a => a.GetStatisticsAsync(It.IsAny<StatisticsQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApplicationStatisticsDto
            {
                TotalApplications = 0,
                ByStatus = Enum.GetValues<ApplicationStatus>().Select(s => new StatusCountDto { Status = s }).ToList()
            });
        var client = await AdminAsync();

        var response = await client.GetAsync("/Admin/Statistics?from=2020-01-01&to=2020-01-31");
        var html = await WebFactory.BodyAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"statTotal\">0<", html);
        Assert.Contains("Valitud perioodil avaldusi ei ole", html);
        Assert.Contains("width: 0%", html);
    }

    [Fact]
    public async Task AdminNavigation_ContainsStatistics_ForAdminsOnly()
    {
        _factory.Api.Setup(a => a.GetStatisticsAsync(It.IsAny<StatisticsQueryParameters>(), It.IsAny<CancellationToken>())).ReturnsAsync(Stats());
        var admin = await AdminAsync();
        Assert.Contains("/Admin/Statistics", await WebFactory.BodyAsync(await admin.GetAsync("/Admin/Statistics")));

        _factory.Api.Setup(a => a.GetMyApplicationsAsync(It.IsAny<ApplicationQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebFactory.Page(Array.Empty<ApplicationDto>(), 0));
        var student = _factory.CreateBrowser();
        await _factory.LoginAsync(student, UserRole.Student);
        Assert.DoesNotContain("/Admin/Statistics", await WebFactory.BodyAsync(await student.GetAsync("/Student/Applications")));
    }

    [Theory]
    [InlineData(5, 10, "50")]
    [InlineData(10, 10, "100")]
    [InlineData(1, 3, "33.3")]
    [InlineData(0, 10, "0")]
    [InlineData(5, 0, "0")]
    public void Percent_IsInvariantAndSafe(int value, int max, string expected) =>
        Assert.Equal(expected, StatisticsViewModel.Percent(value, max));

    [Fact]
    public void Max_NeverReturnsLessThanOne()
    {
        Assert.Equal(1, StatisticsViewModel.Max([]));
        Assert.Equal(1, StatisticsViewModel.Max([0, 0]));
        Assert.Equal(7, StatisticsViewModel.Max([3, 7, 1]));
    }
}
