using System.Net;
using System.Text.RegularExpressions;
using Moq;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Web.Services;

namespace Praktikaportaal.Web.Tests;

/// <summary>Company area: access, dashboard, create/edit form, applicants and status transitions, moderation.</summary>
public class CompanyPagesTests : IClassFixture<WebFactory>
{
    private readonly WebFactory _factory;
    private readonly Guid _companyId = Guid.NewGuid();

    private static readonly Dictionary<string, Guid> SkillIds = new() { ["C#"] = Guid.NewGuid(), ["SQL"] = Guid.NewGuid(), ["Git"] = Guid.NewGuid() };

    public CompanyPagesTests(WebFactory factory)
    {
        _factory = factory;
        _factory.Api.Reset();
        _factory.Api.Setup(a => a.GetNotificationsAsync(It.IsAny<NotificationQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationListDto());
        _factory.Api.Setup(a => a.GetSkillsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SkillIds.Select(s => new SkillDto { Id = s.Value, Name = s.Key }).ToList());
        _factory.Api.Setup(a => a.GetInternshipFiltersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InternshipFiltersDto { Fields = ["IT", "Design"], Locations = ["Tallinn"] });
    }

    private async Task<HttpClient> CompanyAsync()
    {
        var client = _factory.CreateBrowser();
        await _factory.LoginAsync(client, UserRole.Company, _companyId, "Hr", "Boss");
        return client;
    }

    private InternshipDto Mine(string title = "My Internship", InternshipStatus status = InternshipStatus.Published, int applications = 0)
    {
        var i = WebFactory.Internship(title);
        i.CompanyProfileId = _companyId;
        i.Status = status;
        i.ApplicationsCount = applications;
        return i;
    }

    private static ApplicationDto Applicant(ApplicationStatus status, string name = "Mari Tamm", bool hasCv = true) => new()
    {
        Id = Guid.NewGuid(), StudentName = name, StudentEmail = "mari@student.ee", SchoolName = "TTHK", StudyProgram = "IT", Course = 2,
        CoverLetter = "Hello, I would like to join.", Status = status, HasCv = hasCv, AppliedAt = DateTime.UtcNow
    };

    private static Dictionary<string, string> ValidForm(params string[] skillIds)
    {
        var form = new Dictionary<string, string>
        {
            ["Title"] = "Backend Intern", ["Description"] = "A long enough description of the job.", ["Field"] = "IT", ["Location"] = "Tallinn"
        };
        return form;
    }

    // ── access ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("/Company")]
    [InlineData("/Company/Create")]
    [InlineData("/Company/Edit/00000000-0000-0000-0000-000000000001")]
    [InlineData("/Company/Applicants/00000000-0000-0000-0000-000000000001")]
    public async Task Anonymous_IsSentToLogin(string url)
    {
        var response = await _factory.CreateBrowser().GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login", WebFactory.LocationOf(response));
    }

    [Theory]
    [InlineData(UserRole.Student)]
    [InlineData(UserRole.Admin)]
    public async Task OtherRoles_AreDeniedOnCompanyPages(UserRole role)
    {
        var client = _factory.CreateBrowser();
        await _factory.LoginAsync(client, role);

        var response = await client.GetAsync("/Company");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", WebFactory.LocationOf(response));
    }

    [Fact]
    public async Task Student_CannotChangeOrDeleteInternships()
    {
        var client = _factory.CreateBrowser();
        await _factory.LoginAsync(client, UserRole.Student);
        _factory.Api.Setup(a => a.SearchInternshipsAsync(It.IsAny<InternshipQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebFactory.Page(Array.Empty<InternshipDto>(), 0));

        var id = Guid.NewGuid();
        var status = await WebFactory.PostFormAsync(client, $"/Internships/SetStatus/{id}", new() { ["status"] = "Closed" }, formPage: "/Internships");
        var delete = await WebFactory.PostFormAsync(client, $"/Internships/Delete/{id}", new(), formPage: "/Internships");

        Assert.Contains("AccessDenied", WebFactory.LocationOf(status));
        Assert.Contains("AccessDenied", WebFactory.LocationOf(delete));
        _factory.Api.Verify(a => a.SetInternshipStatusAsync(It.IsAny<Guid>(), It.IsAny<InternshipStatus>(), It.IsAny<CancellationToken>()), Times.Never);
        _factory.Api.Verify(a => a.DeleteInternshipAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── dashboard ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Dashboard_ListsInternships_WithCountsAndLiveElements()
    {
        var published = Mine("Published One", applications: 3);
        var draft = Mine("Draft One", InternshipStatus.Draft, applications: 1);
        _factory.Api.Setup(a => a.GetMyInternshipsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([published, draft]);
        var client = await CompanyAsync();

        var html = await WebFactory.BodyAsync(await client.GetAsync("/Company"));

        Assert.Contains("Published One", html);
        Assert.Contains("Mustand", html);                                                  // Draft label
        Assert.Contains($"data-applications-count=\"{published.Id}\"", html);              // target of live counter updates
        Assert.Contains("id=\"totalApplications\">4<", html);                              // 3 + 1
        Assert.Contains("id=\"liveStatus\"", html);
        Assert.Contains("id=\"liveFeed\"", html);
        Assert.Contains("live-applications.js", html);
        Assert.Contains("company-dashboard.js", html);
        // status change offers every status except the current one
        Assert.Contains("Muuda staatuseks: Suletud", html);
    }

    [Fact]
    public async Task Dashboard_Empty_ShowsCallToAction()
    {
        _factory.Api.Setup(a => a.GetMyInternshipsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var client = await CompanyAsync();

        Assert.Contains("Sul ei ole veel ühtegi kuulutust", await WebFactory.BodyAsync(await client.GetAsync("/Company")));
    }

    // ── create ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_SendsAllFieldsAndSkillIds_BlankOptionalsBecomeNull()
    {
        CreateInternshipDto? sent = null;
        _factory.Api.Setup(a => a.CreateInternshipAsync(It.IsAny<CreateInternshipDto>(), It.IsAny<CancellationToken>()))
            .Callback<CreateInternshipDto, CancellationToken>((dto, _) => sent = dto)
            .ReturnsAsync(Mine("Created"));
        var client = await CompanyAsync();
        var future = DateTime.UtcNow.AddMonths(3);

        var form = ValidForm();
        form["Requirements"] = "   ";
        form["StartDate"] = future.ToString("yyyy-MM-dd");
        form["EndDate"] = future.AddMonths(2).ToString("yyyy-MM-dd");
        form["ApplicationDeadline"] = future.AddDays(-20).ToString("yyyy-MM-dd");
        form["IsRemote"] = "true";
        var body = form.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)).ToList();
        body.Add(new("SkillIds", SkillIds["C#"].ToString()));
        body.Add(new("SkillIds", SkillIds["Git"].ToString()));
        body.Add(new("SkillIds", SkillIds["C#"].ToString())); // duplicates are removed

        string html = await client.GetStringAsync("/Company/Create");
        body.Add(new("__RequestVerificationToken", WebFactory.FindToken(html)!));
        var response = await client.PostAsync("/Company/Create", new FormUrlEncodedContent(body));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Internships/Details/", WebFactory.LocationOf(response));
        Assert.Equal("Backend Intern", sent!.Title);
        Assert.Equal("IT", sent.Field);
        Assert.Null(sent.Requirements);
        Assert.True(sent.IsRemote);
        Assert.Equal([SkillIds["C#"], SkillIds["Git"]], sent.SkillIds);
    }

    [Fact]
    public async Task Create_InvalidFields_ShowEstonianMessages_AndDoNotCallTheApi()
    {
        var client = await CompanyAsync();

        var response = await WebFactory.PostFormAsync(client, "/Company/Create", new() { ["Title"] = "ab", ["Description"] = "short", ["Field"] = "" });

        var html = await WebFactory.BodyAsync(response);
        Assert.Contains("Pealkiri peab olema 3 kuni 200 märki", html);
        Assert.Contains("Kirjeldus peab olema 10 kuni 5000 märki", html);
        Assert.Contains("Vali või sisesta valdkond", html);
        _factory.Api.Verify(a => a.CreateInternshipAsync(It.IsAny<CreateInternshipDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_DateRules_AreChecked()
    {
        var client = await CompanyAsync();
        var form = ValidForm();
        form["StartDate"] = "2030-03-01";
        form["EndDate"] = "2030-02-01";           // before the start
        form["ApplicationDeadline"] = "2030-04-01"; // after the start

        var html = await WebFactory.BodyAsync(await WebFactory.PostFormAsync(client, "/Company/Create", form));

        Assert.Contains("Lõppkuupäev peab olema pärast alguskuupäeva", html);
        Assert.Contains("Avalduste tähtaeg peab olema enne praktika algust", html);

        form = ValidForm();
        form["ApplicationDeadline"] = "2020-01-01"; // new listing: not in the past
        html = await WebFactory.BodyAsync(await WebFactory.PostFormAsync(client, "/Company/Create", form));
        Assert.Contains("Avalduste tähtaeg ei tohi olla minevikus", html);
        _factory.Api.Verify(a => a.CreateInternshipAsync(It.IsAny<CreateInternshipDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_ApiRejection_IsShownOnTheForm_AndTheInputIsKept()
    {
        _factory.Api.Setup(a => a.CreateInternshipAsync(It.IsAny<CreateInternshipDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException(HttpStatusCode.BadRequest, "Business rule violation", "Unknown skill id(s): 123."));
        var client = await CompanyAsync();

        var response = await WebFactory.PostFormAsync(client, "/Company/Create", ValidForm());

        var html = await WebFactory.BodyAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Valitud oskust ei leitud", html);
        Assert.Contains("value=\"Backend Intern\"", html);
    }

    // ── edit ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Edit_Form_IsPrefilled_SkillNamesBecomeTickedCheckboxes()
    {
        var internship = Mine("Existing");
        internship.Skills = ["C#", "SQL"];
        _factory.Api.Setup(a => a.GetInternshipAsync(internship.Id, It.IsAny<CancellationToken>())).ReturnsAsync(internship);
        var client = await CompanyAsync();

        var html = await WebFactory.BodyAsync(await client.GetAsync($"/Company/Edit/{internship.Id}"));

        Assert.Contains("value=\"Existing\"", html);
        Assert.Equal(2, Regex.Matches(html, "name=\"SkillIds\"[^>]*checked").Count);
        Assert.Contains("Salvesta muudatused", html);
    }

    [Fact]
    public async Task Edit_SomeoneElsesInternship_IsAccessDenied()
    {
        var foreign = WebFactory.Internship("Not mine"); // another company id
        _factory.Api.Setup(a => a.GetInternshipAsync(foreign.Id, It.IsAny<CancellationToken>())).ReturnsAsync(foreign);
        var client = await CompanyAsync();

        var response = await client.GetAsync($"/Company/Edit/{foreign.Id}");

        Assert.Contains("AccessDenied", WebFactory.LocationOf(response));
    }

    [Fact]
    public async Task Edit_Post_CallsUpdate_AndRedirectsToTheDashboard()
    {
        var id = Guid.NewGuid();
        UpdateInternshipDto? sent = null;
        _factory.Api.Setup(a => a.UpdateInternshipAsync(id, It.IsAny<UpdateInternshipDto>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, UpdateInternshipDto, CancellationToken>((_, dto, _) => sent = dto)
            .ReturnsAsync(Mine());
        _factory.Api.Setup(a => a.GetInternshipAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(Mine());
        var client = await CompanyAsync();
        var form = ValidForm();
        form["Title"] = "Renamed";
        // an old deadline may stay on an existing listing (only new listings must not use a past date)
        form["ApplicationDeadline"] = "2020-01-01";

        var response = await WebFactory.PostFormAsync(client, $"/Company/Edit/{id}", form, formPage: $"/Company/Edit/{id}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Company", WebFactory.LocationOf(response));
        Assert.Equal("Renamed", sent!.Title);
    }

    // ── applicants ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ApplicationStatus.Submitted, new[] { "Märgi vaadatuks", "Lükka tagasi" })]
    [InlineData(ApplicationStatus.Viewed, new[] { "Kutsu intervjuule", "Lükka tagasi" })]
    [InlineData(ApplicationStatus.Interview, new[] { "Aktsepteeri", "Lükka tagasi" })]
    [InlineData(ApplicationStatus.Accepted, new string[0])]
    [InlineData(ApplicationStatus.Rejected, new string[0])]
    [InlineData(ApplicationStatus.Withdrawn, new string[0])]
    public async Task Applicants_OnlyOfferTheAllowedTransitions(ApplicationStatus current, string[] expectedButtons)
    {
        var internship = Mine("With Applicants", applications: 1);
        _factory.Api.Setup(a => a.GetInternshipAsync(internship.Id, It.IsAny<CancellationToken>())).ReturnsAsync(internship);
        _factory.Api.Setup(a => a.GetInternshipApplicationsAsync(internship.Id, It.IsAny<ApplicationQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebFactory.Page([Applicant(current)], 1, pageSize: 10));
        var client = await CompanyAsync();

        var html = await WebFactory.BodyAsync(await client.GetAsync($"/Company/Applicants/{internship.Id}"));

        var buttons = Regex.Matches(html, "<button type=\"submit\" class=\"btn btn-sm[^\"]*\"[^>]*>\\s*([^<]+?)\\s*</button>")
            .Select(m => m.Groups[1].Value.Trim()).Where(t => t != "Filtreeri").ToHashSet();
        Assert.Equal(expectedButtons.ToHashSet(), buttons);
    }

    [Fact]
    public async Task Applicants_ShowDetails_CvLink_AndLiveListMarkers()
    {
        var internship = Mine("Listing", applications: 2);
        var withCv = Applicant(ApplicationStatus.Submitted, "Has Cv");
        var noCv = Applicant(ApplicationStatus.Submitted, "No Cv", hasCv: false);
        _factory.Api.Setup(a => a.GetInternshipAsync(internship.Id, It.IsAny<CancellationToken>())).ReturnsAsync(internship);
        _factory.Api.Setup(a => a.GetInternshipApplicationsAsync(internship.Id, It.IsAny<ApplicationQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebFactory.Page([withCv, noCv], 2, pageSize: 10));
        var client = await CompanyAsync();

        var html = await WebFactory.BodyAsync(await client.GetAsync($"/Company/Applicants/{internship.Id}"));

        Assert.Contains("Has Cv", html);
        Assert.Contains("Hello, I would like to join.", html);
        Assert.Contains($"/Company/DownloadCv/{withCv.Id}", html);
        Assert.DoesNotContain($"/Company/DownloadCv/{noCv.Id}", html);
        Assert.Contains("CV puudub", html);
        Assert.Contains($"id=\"applicantsList\" data-internship-id=\"{internship.Id}\"", html);   // refreshed in place by the live script
        Assert.Contains($"data-application-id=\"{withCv.Id}\"", html);
        Assert.Contains("company-applicants.js", html);
    }

    [Fact]
    public async Task Applicants_StatusFilter_IsPassedToTheApi()
    {
        var internship = Mine();
        ApplicationQueryParameters? sent = null;
        _factory.Api.Setup(a => a.GetInternshipAsync(internship.Id, It.IsAny<CancellationToken>())).ReturnsAsync(internship);
        _factory.Api.Setup(a => a.GetInternshipApplicationsAsync(internship.Id, It.IsAny<ApplicationQueryParameters>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, ApplicationQueryParameters, CancellationToken>((_, q, _) => sent = q)
            .ReturnsAsync(WebFactory.Page(Array.Empty<ApplicationDto>(), 0));
        var client = await CompanyAsync();

        var html = await WebFactory.BodyAsync(await client.GetAsync($"/Company/Applicants/{internship.Id}?status=Interview&page=2"));

        Assert.Equal(ApplicationStatus.Interview, sent!.Status);
        Assert.Equal(2, sent.Page);
        Assert.Contains("Selle staatusega avaldusi ei ole", html);
    }

    [Fact]
    public async Task Applicants_OfAnotherCompany_IsAccessDenied()
    {
        var foreign = WebFactory.Internship();
        _factory.Api.Setup(a => a.GetInternshipAsync(foreign.Id, It.IsAny<CancellationToken>())).ReturnsAsync(foreign);
        var client = await CompanyAsync();

        var response = await client.GetAsync($"/Company/Applicants/{foreign.Id}");

        Assert.Contains("AccessDenied", WebFactory.LocationOf(response));
        _factory.Api.Verify(a => a.GetInternshipApplicationsAsync(It.IsAny<Guid>(), It.IsAny<ApplicationQueryParameters>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetApplicationStatus_CallsTheApi_AndComesBackToTheList()
    {
        var applicationId = Guid.NewGuid();
        var internshipId = Guid.NewGuid();
        var client = await CompanyAsync();

        var response = await WebFactory.PostFormAsync(client, $"/Company/SetApplicationStatus/{applicationId}",
            new() { ["status"] = "Interview", ["internshipId"] = internshipId.ToString(), ["filter"] = "Viewed", ["page"] = "2" }, formPage: "/Company/Create");

        _factory.Api.Verify(a => a.SetApplicationStatusAsync(applicationId, ApplicationStatus.Interview, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        string location = WebFactory.LocationOf(response);
        Assert.StartsWith($"/Company/Applicants/{internshipId}", location);
        Assert.Contains("status=Viewed", location);   // the filter and page survive
        Assert.Contains("page=2", location);
    }

    [Fact]
    public async Task SetApplicationStatus_ForbiddenTransition_IsExplainedInEstonian()
    {
        var applicationId = Guid.NewGuid();
        var internship = Mine();
        _factory.Api.Setup(a => a.SetApplicationStatusAsync(applicationId, It.IsAny<ApplicationStatus>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException(HttpStatusCode.BadRequest, "Business rule violation", "Cannot change application status from Submitted to Accepted. Allowed next statuses: Viewed, Rejected."));
        _factory.Api.Setup(a => a.GetInternshipAsync(internship.Id, It.IsAny<CancellationToken>())).ReturnsAsync(internship);
        _factory.Api.Setup(a => a.GetInternshipApplicationsAsync(internship.Id, It.IsAny<ApplicationQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebFactory.Page(Array.Empty<ApplicationDto>(), 0));
        var client = await CompanyAsync();

        await WebFactory.PostFormAsync(client, $"/Company/SetApplicationStatus/{applicationId}",
            new() { ["status"] = "Accepted", ["internshipId"] = internship.Id.ToString() }, formPage: "/Company/Create");

        Assert.Contains("Avalduse staatust ei saa sellele väärtusele muuta", await WebFactory.BodyAsync(await client.GetAsync($"/Company/Applicants/{internship.Id}")));
    }

    [Fact]
    public async Task DownloadCv_StreamsTheApplicantsFile()
    {
        var id = Guid.NewGuid();
        _factory.Api.Setup(a => a.DownloadApplicantCvAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiFile("%PDF-1.4"u8.ToArray(), "Opilase CV.pdf", "application/pdf"));
        var client = await CompanyAsync();

        var response = await client.GetAsync($"/Company/DownloadCv/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task DownloadCv_ForbiddenByTheApi_BecomesAccessDenied()
    {
        var id = Guid.NewGuid();
        _factory.Api.Setup(a => a.DownloadApplicantCvAsync(id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException(HttpStatusCode.Forbidden, "Forbidden", "Only the company that received the application can download the CV."));
        var client = await CompanyAsync();

        var response = await client.GetAsync($"/Company/DownloadCv/{id}");

        Assert.Contains("AccessDenied", WebFactory.LocationOf(response));
    }

    // ── status change & delete (company) and moderation (admin) ─────────────

    [Fact]
    public async Task Company_ChangesStatus_AndDeletes()
    {
        _factory.Api.Setup(a => a.GetMyInternshipsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var id = Guid.NewGuid();
        var client = await CompanyAsync();

        var status = await WebFactory.PostFormAsync(client, $"/Internships/SetStatus/{id}", new() { ["status"] = "Closed" }, formPage: "/Company");
        var delete = await WebFactory.PostFormAsync(client, $"/Internships/Delete/{id}", new(), formPage: "/Company");

        _factory.Api.Verify(a => a.SetInternshipStatusAsync(id, InternshipStatus.Closed, It.IsAny<CancellationToken>()), Times.Once);
        _factory.Api.Verify(a => a.DeleteInternshipAsync(id, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("/Company", WebFactory.LocationOf(status));
        Assert.Equal("/Company", WebFactory.LocationOf(delete));
    }

    [Fact]
    public async Task Admin_SeesModerationCard_AndCanChangeStatus_ButNotEditOrSeeApplicants()
    {
        var internship = WebFactory.Internship("Moderated");
        _factory.Api.Setup(a => a.GetInternshipAsync(internship.Id, It.IsAny<CancellationToken>())).ReturnsAsync(internship);
        var client = _factory.CreateBrowser();
        await _factory.LoginAsync(client, UserRole.Admin);

        var html = await WebFactory.BodyAsync(await client.GetAsync($"/Internships/Details/{internship.Id}"));
        Assert.Contains("Modereerimine", html);
        Assert.Contains("Kustuta kuulutus", html);
        Assert.DoesNotContain("Vaata avaldusi", html);

        var response = await WebFactory.PostFormAsync(client, $"/Internships/SetStatus/{internship.Id}",
            new() { ["status"] = "Closed", ["returnTo"] = "details" }, formPage: $"/Internships/Details/{internship.Id}");

        _factory.Api.Verify(a => a.SetInternshipStatusAsync(internship.Id, InternshipStatus.Closed, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal($"/Internships/Details/{internship.Id}", WebFactory.LocationOf(response));
    }

    [Fact]
    public async Task OwnerSeesManageCard_ButOtherCompaniesDoNot()
    {
        var mine = Mine("Mine");
        var foreign = WebFactory.Internship("Foreign");
        _factory.Api.Setup(a => a.GetInternshipAsync(mine.Id, It.IsAny<CancellationToken>())).ReturnsAsync(mine);
        _factory.Api.Setup(a => a.GetInternshipAsync(foreign.Id, It.IsAny<CancellationToken>())).ReturnsAsync(foreign);
        var client = await CompanyAsync();

        Assert.Contains("Halda kuulutust", await WebFactory.BodyAsync(await client.GetAsync($"/Internships/Details/{mine.Id}")));
        Assert.DoesNotContain("Halda kuulutust", await WebFactory.BodyAsync(await client.GetAsync($"/Internships/Details/{foreign.Id}")));
    }
}
