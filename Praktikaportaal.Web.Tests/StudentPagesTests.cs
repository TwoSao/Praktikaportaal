using System.Net;
using System.Net.Http.Headers;
using Moq;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Web.Services;

namespace Praktikaportaal.Web.Tests;

/// <summary>Student area: access rules, profile and skills, CV, applying, applications, recommendations, notifications.</summary>
public class StudentPagesTests : IClassFixture<WebFactory>
{
    private readonly WebFactory _factory;
    private readonly Guid _profileId = Guid.NewGuid();

    public StudentPagesTests(WebFactory factory)
    {
        _factory = factory;
        _factory.Api.Reset();
        // The navbar asks for the unread count on every page of a logged-in user.
        _factory.Api.Setup(a => a.GetNotificationsAsync(It.IsAny<NotificationQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationListDto { UnreadCount = 0 });
    }

    private async Task<HttpClient> StudentAsync()
    {
        var client = _factory.CreateBrowser();
        await _factory.LoginAsync(client, UserRole.Student, _profileId, "Mari", "Tamm");
        return client;
    }

    private static StudentProfileDto Profile(params (string Name, SkillLevel Level)[] skills) => new()
    {
        Id = Guid.NewGuid(), Email = "mari@student.ee", FirstName = "Mari", LastName = "Tamm", SchoolName = "TTHK", StudyProgram = "IT", Course = 2,
        Skills = skills.Select(s => new StudentSkillDto { SkillId = SkillIds[s.Name], Name = s.Name, Level = s.Level }).ToList()
    };

    private static readonly Dictionary<string, Guid> SkillIds = new() { ["C#"] = Guid.NewGuid(), ["SQL"] = Guid.NewGuid(), ["Git"] = Guid.NewGuid() };

    private static IReadOnlyList<SkillDto> AllSkills() => SkillIds.Select(s => new SkillDto { Id = s.Value, Name = s.Key }).ToList();

    // ── access ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("/Student/Profile")]
    [InlineData("/Student/Applications")]
    [InlineData("/Student/Recommendations")]
    [InlineData("/Notifications")]
    public async Task Anonymous_IsSentToLogin_WithReturnUrl(string url)
    {
        var response = await _factory.CreateBrowser().GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login", WebFactory.LocationOf(response));
        Assert.Contains("ReturnUrl", WebFactory.LocationOf(response));
    }

    [Theory]
    [InlineData(UserRole.Company)]
    [InlineData(UserRole.Admin)]
    public async Task OtherRoles_AreDeniedOnStudentPages(UserRole role)
    {
        var client = _factory.CreateBrowser();
        await _factory.LoginAsync(client, role);

        var response = await client.GetAsync("/Student/Profile");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", WebFactory.LocationOf(response));
    }

    [Fact]
    public async Task OtherRoles_CannotApply()
    {
        var client = _factory.CreateBrowser();
        await _factory.LoginAsync(client, UserRole.Company);

        var response = await client.GetAsync($"/Internships/Apply/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("AccessDenied", WebFactory.LocationOf(response));
        _factory.Api.Verify(a => a.ApplyAsync(It.IsAny<Guid>(), It.IsAny<CreateApplicationDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── profile & skills ────────────────────────────────────────────────────

    [Fact]
    public async Task Profile_ShowsAllSkills_WithTheStudentsOnesTickedAtTheirLevel()
    {
        _factory.Api.Setup(a => a.GetMyProfileAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Profile(("C#", SkillLevel.Advanced)));
        _factory.Api.Setup(a => a.GetSkillsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(AllSkills());
        var client = await StudentAsync();

        var html = await WebFactory.BodyAsync(await client.GetAsync("/Student/Profile"));

        Assert.Contains("Minu profiil", html);
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(html, "class=\"form-check-input skill-check\"").Count); // one checkbox per skill
        Assert.Contains("value=\"Mari\"", html);
        Assert.Matches("<option value=\"Advanced\" selected", html);
        Assert.Contains("CV-d pole veel üles laaditud", html);
    }

    [Fact]
    public async Task Profile_Post_SendsOnlyTickedSkills_WithLevels()
    {
        UpdateStudentProfileDto? sent = null;
        _factory.Api.Setup(a => a.GetMyProfileAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Profile());
        _factory.Api.Setup(a => a.GetSkillsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(AllSkills());
        _factory.Api.Setup(a => a.UpdateMyProfileAsync(It.IsAny<UpdateStudentProfileDto>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateStudentProfileDto, CancellationToken>((dto, _) => sent = dto)
            .ReturnsAsync(Profile());
        var client = await StudentAsync();

        var response = await WebFactory.PostFormAsync(client, "/Student/Profile", new()
        {
            ["FirstName"] = "Mari-Liis", ["LastName"] = "Tamm", ["SchoolName"] = "TalTech", ["StudyProgram"] = "Informatics", ["Course"] = "3",
            ["Skills[0].SkillId"] = SkillIds["C#"].ToString(), ["Skills[0].Name"] = "C#", ["Skills[0].Selected"] = "true", ["Skills[0].Level"] = "Expert",
            ["Skills[1].SkillId"] = SkillIds["SQL"].ToString(), ["Skills[1].Name"] = "SQL", ["Skills[1].Selected"] = "false", ["Skills[1].Level"] = "Beginner",
            ["Skills[2].SkillId"] = SkillIds["Git"].ToString(), ["Skills[2].Name"] = "Git", ["Skills[2].Selected"] = "true", ["Skills[2].Level"] = "Intermediate"
        }, formPage: "/Student/Profile");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("Mari-Liis", sent!.FirstName);
        Assert.Equal(3, sent.Course);
        Assert.Equal([(SkillIds["C#"], SkillLevel.Expert), (SkillIds["Git"], SkillLevel.Intermediate)], sent.Skills.Select(s => (s.SkillId, s.Level)));
    }

    [Fact]
    public async Task Profile_Post_Invalid_ShowsErrors_AndDoesNotCallTheApiUpdate()
    {
        _factory.Api.Setup(a => a.GetSkillsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(AllSkills());
        _factory.Api.Setup(a => a.GetMyCvAsync(It.IsAny<CancellationToken>())).ReturnsAsync((CvDto?)null);
        var client = await StudentAsync();
        _factory.Api.Setup(a => a.GetMyProfileAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Profile());

        var response = await WebFactory.PostFormAsync(client, "/Student/Profile",
            new() { ["FirstName"] = "", ["LastName"] = "Tamm", ["SchoolName"] = "S", ["StudyProgram"] = "P", ["Course"] = "99" }, formPage: "/Student/Profile");

        var html = await WebFactory.BodyAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Sisesta eesnimi", html);
        Assert.Contains("Kursus peab olema 1 kuni 6", html);
        _factory.Api.Verify(a => a.UpdateMyProfileAsync(It.IsAny<UpdateStudentProfileDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── CV ──────────────────────────────────────────────────────────────────

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string fileName, byte[] bytes, string contentType)
    {
        string token = WebFactory.FindToken(await client.GetStringAsync("/Student/Profile"))!;
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return await client.PostAsync("/Student/UploadCv", new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { file, "file", fileName }
        });
    }

    private void ProfileSetup()
    {
        _factory.Api.Setup(a => a.GetMyProfileAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Profile());
        _factory.Api.Setup(a => a.GetSkillsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(AllSkills());
    }

    [Fact]
    public async Task CvUpload_SendsTheFileToTheApi()
    {
        ProfileSetup();
        string? name = null, type = null;
        byte[]? content = null;
        _factory.Api.Setup(a => a.UploadMyCvAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, string, string?, CancellationToken>((s, n, t, _) => { name = n; type = t; using var ms = new MemoryStream(); s.CopyTo(ms); content = ms.ToArray(); })
            .ReturnsAsync(new CvDto { FileName = "cv.pdf" });
        var client = await StudentAsync();

        var response = await UploadAsync(client, "cv.pdf", "%PDF-1.4 body"u8.ToArray(), "application/pdf");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("cv.pdf", name);
        Assert.Equal("application/pdf", type);
        Assert.Equal("%PDF-1.4 body"u8.ToArray(), content);
    }

    [Fact]
    public async Task CvUpload_TooLarge_IsStoppedBeforeTheApi()
    {
        ProfileSetup();
        var client = await StudentAsync();

        var response = await UploadAsync(client, "big.pdf", new byte[5 * 1024 * 1024 + 1], "application/pdf");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        _factory.Api.Verify(a => a.UploadMyCvAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains("liiga suur", await WebFactory.BodyAsync(await client.GetAsync("/Student/Profile")));
    }

    [Fact]
    public async Task CvUpload_ApiRejection_IsShownInEstonian()
    {
        ProfileSetup();
        _factory.Api.Setup(a => a.UploadMyCvAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException(HttpStatusCode.BadRequest, "Business rule violation", "The file content is not a valid .pdf document."));
        var client = await StudentAsync();

        await UploadAsync(client, "fake.pdf", "MZ"u8.ToArray(), "application/pdf");

        Assert.Contains("Faili sisu ei ole korrektne PDF-dokument", await WebFactory.BodyAsync(await client.GetAsync("/Student/Profile")));
    }

    [Fact]
    public async Task CvDownload_StreamsTheFileAsAttachment()
    {
        _factory.Api.Setup(a => a.DownloadMyCvAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiFile("%PDF-1.4"u8.ToArray(), "Mari CV.pdf", "application/pdf"));
        var client = await StudentAsync();

        var response = await client.GetAsync("/Student/DownloadCv");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    // ── applying ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyPage_ShowsCvStatus_AndSubmitsTheCoverLetter()
    {
        var internship = WebFactory.Internship("Apply Here");
        _factory.Api.Setup(a => a.GetInternshipAsync(internship.Id, It.IsAny<CancellationToken>())).ReturnsAsync(internship);
        _factory.Api.Setup(a => a.GetMyCvAsync(It.IsAny<CancellationToken>())).ReturnsAsync((CvDto?)null);
        CreateApplicationDto? sent = null;
        _factory.Api.Setup(a => a.ApplyAsync(internship.Id, It.IsAny<CreateApplicationDto>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, CreateApplicationDto, CancellationToken>((_, dto, _) => sent = dto)
            .ReturnsAsync(new ApplicationDto());
        var client = await StudentAsync();

        var page = await WebFactory.BodyAsync(await client.GetAsync($"/Internships/Apply/{internship.Id}"));
        Assert.Contains("CV puudub", page);

        var response = await WebFactory.PostFormAsync(client, $"/Internships/Apply/{internship.Id}", new() { ["CoverLetter"] = "  Tere!  " });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Student/Applications", response.Headers.Location!.OriginalString);
        Assert.Equal("Tere!", sent!.CoverLetter);
    }

    [Fact]
    public async Task Apply_Conflict_IsShownInEstonianOnTheForm()
    {
        var internship = WebFactory.Internship();
        _factory.Api.Setup(a => a.GetInternshipAsync(internship.Id, It.IsAny<CancellationToken>())).ReturnsAsync(internship);
        _factory.Api.Setup(a => a.GetMyCvAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new CvDto { FileName = "cv.pdf", UploadedAt = DateTime.UtcNow });
        _factory.Api.Setup(a => a.ApplyAsync(internship.Id, It.IsAny<CreateApplicationDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException(HttpStatusCode.Conflict, "Conflict", "You already have an active application for this internship."));
        var client = await StudentAsync();

        var response = await WebFactory.PostFormAsync(client, $"/Internships/Apply/{internship.Id}", new() { ["CoverLetter"] = "x" }, formPage: $"/Internships/Apply/{internship.Id}");

        var html = await WebFactory.BodyAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Sul on sellele praktikakohale juba aktiivne avaldus", html);
        Assert.Contains("CV on olemas", html);
    }

    [Fact]
    public async Task ApplyPage_ForClosedInternship_RedirectsBackToDetails()
    {
        var closed = WebFactory.Internship();
        closed.Status = InternshipStatus.Closed;
        _factory.Api.Setup(a => a.GetInternshipAsync(closed.Id, It.IsAny<CancellationToken>())).ReturnsAsync(closed);
        _factory.Api.Setup(a => a.GetMyCvAsync(It.IsAny<CancellationToken>())).ReturnsAsync((CvDto?)null);
        var client = await StudentAsync();

        var response = await client.GetAsync($"/Internships/Apply/{closed.Id}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Internships/Details/{closed.Id}", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Details_ForAStudent_ShowsApplyButton_OrTheExistingApplication()
    {
        var internship = WebFactory.Internship("Maybe Applied");
        _factory.Api.Setup(a => a.GetInternshipAsync(internship.Id, It.IsAny<CancellationToken>())).ReturnsAsync(internship);
        _factory.Api.Setup(a => a.GetMyApplicationsAsync(It.IsAny<ApplicationQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebFactory.Page(Array.Empty<ApplicationDto>(), 0));
        var client = await StudentAsync();

        Assert.Contains("Esita avaldus", await WebFactory.BodyAsync(await client.GetAsync($"/Internships/Details/{internship.Id}")));

        _factory.Api.Setup(a => a.GetMyApplicationsAsync(It.IsAny<ApplicationQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebFactory.Page([new ApplicationDto { InternshipId = internship.Id, Status = ApplicationStatus.Interview }], 1));
        var html = await WebFactory.BodyAsync(await client.GetAsync($"/Internships/Details/{internship.Id}"));

        Assert.Contains("Sa oled sellele kohale avalduse esitanud", html);
        Assert.Contains("Intervjuu", html);
        Assert.DoesNotContain("btn-lg w-100", html);
    }

    // ── my applications ─────────────────────────────────────────────────────

    private static ApplicationDto App(ApplicationStatus status, string title = "Backend Intern") => new()
    {
        Id = Guid.NewGuid(), InternshipId = Guid.NewGuid(), InternshipTitle = title, CompanyName = "Acme OÜ", Status = status, AppliedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task Applications_ListsStatuses_AndOnlyOpenOnesCanBeWithdrawn()
    {
        _factory.Api.Setup(a => a.GetMyApplicationsAsync(It.IsAny<ApplicationQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebFactory.Page([App(ApplicationStatus.Submitted, "Open One"), App(ApplicationStatus.Accepted, "Done One")], 2, pageSize: 10));
        var client = await StudentAsync();

        var html = await WebFactory.BodyAsync(await client.GetAsync("/Student/Applications"));

        Assert.Contains("Open One", html);
        Assert.Contains("Aktsepteeritud", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "Võta tagasi")); // only the Submitted one
    }

    [Fact]
    public async Task Applications_StatusFilter_IsPassedToTheApi()
    {
        ApplicationQueryParameters? sent = null;
        _factory.Api.Setup(a => a.GetMyApplicationsAsync(It.IsAny<ApplicationQueryParameters>(), It.IsAny<CancellationToken>()))
            .Callback<ApplicationQueryParameters, CancellationToken>((q, _) => sent = q)
            .ReturnsAsync(WebFactory.Page(Array.Empty<ApplicationDto>(), 0));
        var client = await StudentAsync();

        var html = await WebFactory.BodyAsync(await client.GetAsync("/Student/Applications?status=Rejected&page=2"));

        Assert.Equal(ApplicationStatus.Rejected, sent!.Status);
        Assert.Equal(2, sent.Page);
        Assert.Contains("Selle staatusega avaldusi ei ole", html);
    }

    [Fact]
    public async Task Withdraw_CallsTheApi_AndExplainsFailuresInEstonian()
    {
        var id = Guid.NewGuid();
        var client = await StudentAsync();
        _factory.Api.Setup(a => a.GetMyApplicationsAsync(It.IsAny<ApplicationQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebFactory.Page(Array.Empty<ApplicationDto>(), 0));

        await WebFactory.PostFormAsync(client, $"/Student/Withdraw/{id}", new(), formPage: "/Student/Applications");
        _factory.Api.Verify(a => a.WithdrawApplicationAsync(id, It.IsAny<CancellationToken>()), Times.Once);

        _factory.Api.Setup(a => a.WithdrawApplicationAsync(id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException(HttpStatusCode.BadRequest, "Business rule violation", "An application with status Accepted cannot be withdrawn."));
        await WebFactory.PostFormAsync(client, $"/Student/Withdraw/{id}", new(), formPage: "/Student/Applications");

        Assert.Contains("Seda avaldust ei saa enam tagasi võtta", await WebFactory.BodyAsync(await client.GetAsync("/Student/Applications")));
    }

    // ── recommendations ─────────────────────────────────────────────────────

    [Fact]
    public async Task Recommendations_ShowPercentMatchedAndMissingSkills()
    {
        var profile = Profile(("C#", SkillLevel.Advanced));
        _factory.Api.Setup(a => a.GetMyProfileAsync(It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        RecommendationQueryParameters? sent = null;
        _factory.Api.Setup(a => a.GetRecommendationsAsync(profile.Id, It.IsAny<RecommendationQueryParameters>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, RecommendationQueryParameters, CancellationToken>((_, q, _) => sent = q)
            .ReturnsAsync(WebFactory.Page([new RecommendationDto
            {
                Internship = WebFactory.Internship("Best Match"), OverlapPercent = 66.7, MatchedSkills = ["C#", "SQL"], MissingSkills = ["Docker"]
            }], 1, pageSize: 10));
        var client = await StudentAsync();

        var html = await WebFactory.BodyAsync(await client.GetAsync("/Student/Recommendations?minOverlap=50"));

        Assert.Equal(50, sent!.MinOverlap);
        Assert.Contains("Best Match", html);
        Assert.Contains("66.7 %", html);                        // decimal point, never a comma
        Assert.Contains("width: 66.7%", html);
        Assert.Contains("skill-chip matched", html);
        Assert.Contains("skill-chip missing", html);
    }

    [Fact]
    public async Task Recommendations_WithoutSkills_PointToTheProfile()
    {
        var profile = Profile();
        _factory.Api.Setup(a => a.GetMyProfileAsync(It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _factory.Api.Setup(a => a.GetRecommendationsAsync(profile.Id, It.IsAny<RecommendationQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebFactory.Page(Array.Empty<RecommendationDto>(), 0));
        var client = await StudentAsync();

        Assert.Contains("Lisa oskused profiilis", await WebFactory.BodyAsync(await client.GetAsync("/Student/Recommendations")));
    }

    // ── notifications ───────────────────────────────────────────────────────

    [Fact]
    public async Task Notifications_ListWithUnreadMarkers_MarkReadAndMarkAll()
    {
        var unread = new NotificationDto { Id = Guid.NewGuid(), Title = "Application status updated", Message = "Now: Viewed", CreatedAt = DateTime.UtcNow };
        var list = new NotificationListDto { Items = [unread, new NotificationDto { Id = Guid.NewGuid(), Title = "Old", Message = "m", IsRead = true }], TotalCount = 2, Page = 1, PageSize = 15, UnreadCount = 1 };
        _factory.Api.Setup(a => a.GetNotificationsAsync(It.IsAny<NotificationQueryParameters>(), It.IsAny<CancellationToken>())).ReturnsAsync(list);
        var client = await StudentAsync();

        var html = await WebFactory.BodyAsync(await client.GetAsync("/Notifications"));
        Assert.Contains("Application status updated", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "Märgi loetuks"));    // only for the unread one
        Assert.Contains("id=\"unreadBadge\"", html);                                                   // navbar badge

        await WebFactory.PostFormAsync(client, $"/Notifications/MarkRead/{unread.Id}", new(), formPage: "/Notifications");
        _factory.Api.Verify(a => a.MarkNotificationReadAsync(unread.Id, It.IsAny<CancellationToken>()), Times.Once);

        await WebFactory.PostFormAsync(client, "/Notifications/MarkAllRead", new(), formPage: "/Notifications");
        _factory.Api.Verify(a => a.MarkAllNotificationsReadAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NavbarBadge_FailureDoesNotBreakThePage()
    {
        var client = await StudentAsync();
        _factory.Api.Setup(a => a.GetNotificationsAsync(It.IsAny<NotificationQueryParameters>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiUnavailableException(new HttpRequestException()));
        _factory.Api.Setup(a => a.GetMyApplicationsAsync(It.IsAny<ApplicationQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebFactory.Page(Array.Empty<ApplicationDto>(), 0));

        var response = await client.GetAsync("/Student/Applications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
