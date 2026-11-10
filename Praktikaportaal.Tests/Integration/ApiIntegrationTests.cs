using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Enums;
using Xunit;

namespace Praktikaportaal.Tests.Integration;

/// <summary>
/// End-to-end tests through the real HTTP pipeline: routing, JWT, roles, validation and ProblemDetails.
/// </summary>
public class ApiIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ApiIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json))!;

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    // ── authentication & authorization ──────────────────────────────────────

    [Fact]
    public async Task Anonymous_CanBrowseInternships_ButNotMyApplications()
    {
        var client = _factory.CreateClient();

        var list = await client.GetAsync("/api/internships");
        var mine = await client.GetAsync("/api/applications/my");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, mine.StatusCode);
        Assert.Equal("application/problem+json", mine.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Student_CannotCreateInternship_Forbidden()
    {
        var (student, _) = await _factory.RegisterStudentAsync("it-student1@test.ee");

        var response = await student.PostAsJsonAsync("/api/internships",
            new CreateInternshipDto { Title = "Hacked", Description = "A long enough description.", Field = "IT" }, ApiFactory.Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_Is401ProblemDetails_WithoutLeakingWhichPartWasWrong()
    {
        await _factory.RegisterStudentAsync("it-login@test.ee");

        var wrongPassword = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginDto { Email = "it-login@test.ee", Password = "WrongPass1" }, ApiFactory.Json);
        var unknownUser = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginDto { Email = "nobody@test.ee", Password = "WrongPass1" }, ApiFactory.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal((await ReadJsonAsync(wrongPassword)).GetProperty("detail").GetString(),
                     (await ReadJsonAsync(unknownUser)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Register_DuplicateEmail_Is409()
    {
        await _factory.RegisterStudentAsync("it-dup@test.ee");

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register-student", new RegisterStudentDto
        {
            Email = "IT-DUP@test.ee", Password = "Passw0rd!", FirstName = "A", LastName = "B", SchoolName = "S", StudyProgram = "P", Course = 1
        }, ApiFactory.Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); // e-mail comparison is case-insensitive
    }

    [Fact]
    public async Task Register_InvalidBody_Is400_WithFieldErrors()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register-student", new RegisterStudentDto
        {
            Email = "not-an-email", Password = "short", FirstName = "", LastName = "B", SchoolName = "S", StudyProgram = "P", Course = 99
        }, ApiFactory.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ReadJsonAsync(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("Email", out _));
        Assert.True(errors.TryGetProperty("Password", out _));
        Assert.True(errors.TryGetProperty("FirstName", out _));
        Assert.True(errors.TryGetProperty("Course", out _));
    }

    [Fact]
    public async Task Responses_NeverExposePasswordHashes()
    {
        var (client, auth) = await _factory.RegisterStudentAsync("it-hash@test.ee");

        string me = await (await client.GetAsync("/api/auth/me")).Content.ReadAsStringAsync();
        string profile = await (await client.GetAsync("/api/students/me")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("passwordHash", me, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwordHash", profile, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Student", (await ReadJsonAsync(await client.GetAsync("/api/auth/me"))).GetProperty("role").GetString());
        Assert.NotEqual(Guid.Empty, auth.User.Id);
    }

    // ── internships ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Company_Crud_OwnInternship_AndOtherCompanyIsForbidden()
    {
        var (owner, _) = await _factory.RegisterCompanyAsync("it-owner@test.ee");
        var (other, _) = await _factory.RegisterCompanyAsync("it-other@test.ee");

        var created = await owner.PostAsJsonAsync("/api/internships",
            new CreateInternshipDto { Title = "Original", Description = "A long enough description.", Field = "IT" }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        var internship = await ReadAsync<InternshipDto>(created);

        var updateBody = new UpdateInternshipDto { Title = "Renamed", Description = "A long enough description.", Field = "Design" };
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PutAsJsonAsync($"/api/internships/{internship.Id}", updateBody, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.DeleteAsync($"/api/internships/{internship.Id}")).StatusCode);

        var updated = await owner.PutAsJsonAsync($"/api/internships/{internship.Id}", updateBody, ApiFactory.Json);
        Assert.Equal("Renamed", (await ReadAsync<InternshipDto>(updated)).Title);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/internships/{internship.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync($"/api/internships/{internship.Id}")).StatusCode);
    }

    [Fact]
    public async Task Search_FilterByField_AndPagination_ReturnTotalCount()
    {
        var (company, _) = await _factory.RegisterCompanyAsync("it-search@test.ee");
        for (int i = 1; i <= 5; i++)
            await ApiFactory.CreateInternshipAsync(company, $"Searchable {i}", field: "ZzUniqueField");
        await ApiFactory.CreateInternshipAsync(company, "Other field", field: "Different");

        var page = await ReadAsync<PagedResult<InternshipDto>>(
            await _factory.CreateClient().GetAsync("/api/internships?field=zzuniquefield&pageSize=2&page=3&sort=Title"));

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Single(page.Items);
        Assert.Equal("Searchable 5", page.Items[0].Title);
    }

    [Fact]
    public async Task Search_PageSizeTooLarge_Is400()
    {
        var response = await _factory.CreateClient().GetAsync("/api/internships?pageSize=1000");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── applications ────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplicationFlow_Apply_Duplicate_StatusRules_AndStudentSeesNewStatus()
    {
        var (company, _) = await _factory.RegisterCompanyAsync("it-flow-co@test.ee");
        var internship = await ApiFactory.CreateInternshipAsync(company);
        var (student, _) = await _factory.RegisterStudentAsync("it-flow-st@test.ee");

        var applied = await student.PostAsJsonAsync($"/api/internships/{internship.Id}/applications",
            new CreateApplicationDto { CoverLetter = "Hi" }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Created, applied.StatusCode);
        Assert.NotNull(applied.Headers.Location);
        var application = await ReadAsync<ApplicationDto>(applied);
        Assert.Equal(ApplicationStatus.Submitted, application.Status);

        Assert.Equal(HttpStatusCode.Conflict,
            (await student.PostAsJsonAsync($"/api/internships/{internship.Id}/applications", new CreateApplicationDto(), ApiFactory.Json)).StatusCode);

        // The company sees the applicant; the student may not.
        var applicants = await ReadAsync<PagedResult<ApplicationDto>>(await company.GetAsync($"/api/internships/{internship.Id}/applications"));
        Assert.Single(applicants.Items);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync($"/api/internships/{internship.Id}/applications")).StatusCode);

        // Submitted -> Accepted is not allowed; Submitted -> Viewed is.
        var invalid = await company.PatchAsJsonAsync($"/api/applications/{application.Id}/status", new UpdateApplicationStatusDto { Status = ApplicationStatus.Accepted });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("Viewed", (await ReadJsonAsync(invalid)).GetProperty("detail").GetString());

        var valid = await company.PatchAsJsonAsync($"/api/applications/{application.Id}/status", new UpdateApplicationStatusDto { Status = ApplicationStatus.Viewed });
        Assert.Equal(HttpStatusCode.NoContent, valid.StatusCode);

        var mine = await ReadAsync<PagedResult<ApplicationDto>>(await student.GetAsync("/api/applications/my"));
        Assert.Equal(ApplicationStatus.Viewed, mine.Items.Single().Status);

        // Enums travel as strings.
        Assert.Contains("\"Viewed\"", await (await student.GetAsync("/api/applications/my")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Apply_ToUnknownInternship_Is404()
    {
        var (student, _) = await _factory.RegisterStudentAsync("it-404@test.ee");

        var response = await student.PostAsJsonAsync($"/api/internships/{Guid.NewGuid()}/applications", new CreateApplicationDto(), ApiFactory.Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Notifications_AreCreatedForCompanyAndStudent()
    {
        var (company, _) = await _factory.RegisterCompanyAsync("it-notif-co@test.ee");
        var internship = await ApiFactory.CreateInternshipAsync(company, "Notify me");
        var (student, _) = await _factory.RegisterStudentAsync("it-notif-st@test.ee");
        var application = await ReadAsync<ApplicationDto>(await student.PostAsJsonAsync(
            $"/api/internships/{internship.Id}/applications", new CreateApplicationDto(), ApiFactory.Json));

        var companyInbox = await ReadAsync<NotificationListDto>(await company.GetAsync("/api/notifications/my"));
        Assert.Equal(1, companyInbox.UnreadCount);
        Assert.Contains("Notify me", companyInbox.Items[0].Message);

        await company.PatchAsJsonAsync($"/api/applications/{application.Id}/status", new UpdateApplicationStatusDto { Status = ApplicationStatus.Viewed });
        var studentInbox = await ReadAsync<NotificationListDto>(await student.GetAsync("/api/notifications/my"));
        Assert.Contains("Viewed", studentInbox.Items[0].Message);

        Assert.Equal(HttpStatusCode.NoContent, (await student.PostAsync("/api/notifications/read-all", null)).StatusCode);
        Assert.Equal(0, (await ReadAsync<NotificationListDto>(await student.GetAsync("/api/notifications/my"))).UnreadCount);
    }

    // ── student profile, CV, recommendations ────────────────────────────────

    [Fact]
    public async Task CvUpload_AcceptsPdf_RejectsFakePdf_AndCompanyCanDownloadIt()
    {
        var (company, _) = await _factory.RegisterCompanyAsync("it-cv-co@test.ee");
        var (otherCompany, _) = await _factory.RegisterCompanyAsync("it-cv-other@test.ee");
        var internship = await ApiFactory.CreateInternshipAsync(company);
        var (student, _) = await _factory.RegisterStudentAsync("it-cv-st@test.ee");
        var application = await ReadAsync<ApplicationDto>(await student.PostAsJsonAsync(
            $"/api/internships/{internship.Id}/applications", new CreateApplicationDto(), ApiFactory.Json));

        static MultipartFormDataContent Form(byte[] bytes, string fileName)
        {
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new("application/pdf");
            return new MultipartFormDataContent { { file, "file", fileName } };
        }

        var fake = await student.PostAsync("/api/students/me/cv", Form("MZ not a pdf"u8.ToArray(), "cv.pdf"));
        Assert.Equal(HttpStatusCode.BadRequest, fake.StatusCode);

        byte[] pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<<>>endobj\n%%EOF");
        var uploaded = await student.PostAsync("/api/students/me/cv", Form(pdf, "My CV.pdf"));
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);

        var download = await company.GetAsync($"/api/applications/{application.Id}/cv");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(pdf, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);

        Assert.Equal(HttpStatusCode.Forbidden, (await otherCompany.GetAsync($"/api/applications/{application.Id}/cv")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync($"/api/applications/{application.Id}/cv")).StatusCode);
    }

    [Fact]
    public async Task Profile_UpdateSkills_ThenRecommendationsAreRankedBySkillOverlap()
    {
        var (company, _) = await _factory.RegisterCompanyAsync("it-rec-co@test.ee");
        var skillA = await ReadAsync<SkillDto>(await company.PostAsJsonAsync("/api/skills", new CreateSkillDto { Name = "RecSkillA" }, ApiFactory.Json));
        var skillB = await ReadAsync<SkillDto>(await company.PostAsJsonAsync("/api/skills", new CreateSkillDto { Name = "RecSkillB" }, ApiFactory.Json));

        async Task AddInternshipAsync(string title, params Guid[] skillIds) =>
            (await company.PostAsJsonAsync("/api/internships",
                new CreateInternshipDto { Title = title, Description = "A long enough description.", Field = "IT", SkillIds = [.. skillIds] }, ApiFactory.Json))
            .EnsureSuccessStatusCode();
        await AddInternshipAsync("Needs A only", skillA.Id);
        await AddInternshipAsync("Needs A and B", skillA.Id, skillB.Id);

        var (student, _) = await _factory.RegisterStudentAsync("it-rec-st@test.ee");
        var profile = await ReadAsync<StudentProfileDto>(await student.GetAsync("/api/students/me"));
        var update = await student.PutAsJsonAsync("/api/students/me", new UpdateStudentProfileDto
        {
            FirstName = "Test", LastName = "Student", SchoolName = "TTHK", StudyProgram = "IT", Course = 2,
            Skills = [new StudentSkillInputDto { SkillId = skillA.Id, Level = SkillLevel.Advanced }]
        }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var recs = await ReadAsync<PagedResult<RecommendationDto>>(await student.GetAsync($"/api/students/{profile.Id}/recommendations"));

        Assert.Equal(["Needs A only", "Needs A and B"], recs.Items.Select(r => r.Internship.Title));
        Assert.Equal([100, 50], recs.Items.Select(r => r.OverlapPercent));
        Assert.Equal(["RecSkillB"], recs.Items[1].MissingSkills);

        var (otherStudent, _) = await _factory.RegisterStudentAsync("it-rec-other@test.ee");
        Assert.Equal(HttpStatusCode.Forbidden, (await otherStudent.GetAsync($"/api/students/{profile.Id}/recommendations")).StatusCode);
    }

    // ── admin & service roles ───────────────────────────────────────────────

    [Fact]
    public async Task AdminStatistics_AdminOnly_AndCountsApplications()
    {
        var (company, _) = await _factory.RegisterCompanyAsync("it-stat-co@test.ee");
        var internship = await ApiFactory.CreateInternshipAsync(company, "Stats target", "StatsField");
        var (student, _) = await _factory.RegisterStudentAsync("it-stat-st@test.ee");
        await student.PostAsJsonAsync($"/api/internships/{internship.Id}/applications", new CreateApplicationDto(), ApiFactory.Json);
        var admin = await _factory.LoginAsPrivilegedUserAsync(UserRole.Admin, "it-admin@test.ee");

        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/admin/statistics")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await company.GetAsync("/api/admin/statistics")).StatusCode);

        var stats = await ReadAsync<ApplicationStatisticsDto>(await admin.GetAsync("/api/admin/statistics?period=Day"));
        Assert.True(stats.TotalApplications >= 1);
        Assert.Contains(stats.ByField, f => f.Field == "StatsField" && f.Count == 1);
        Assert.Equal(stats.TotalApplications, stats.ByStatus.Sum(s => s.Count));
    }

    [Fact]
    public async Task ServiceAccount_CanListStudents_AndCreateNotifications_ButStudentsCannot()
    {
        var (student, auth) = await _factory.RegisterStudentAsync("it-svc-st@test.ee");
        var service = await _factory.LoginAsPrivilegedUserAsync(UserRole.Service, "it-service@test.ee");

        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/students")).StatusCode);
        var students = await ReadAsync<PagedResult<StudentProfileDto>>(await service.GetAsync("/api/students"));
        Assert.Contains(students.Items, s => s.UserId == auth.User.Id);

        var created = await service.PostAsJsonAsync("/api/notifications",
            new CreateNotificationDto { UserId = auth.User.Id, Title = "Digest", Message = "2 new internships" }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.PostAsJsonAsync("/api/notifications",
            new CreateNotificationDto { UserId = auth.User.Id, Title = "Spam", Message = "x" }, ApiFactory.Json)).StatusCode);

        var inbox = await ReadAsync<NotificationListDto>(await student.GetAsync("/api/notifications/my"));
        Assert.Equal("Digest", inbox.Items.Single().Title);
    }

    [Fact]
    public async Task ExpiredOrTamperedToken_Is401()
    {
        var (client, auth) = await _factory.RegisterStudentAsync("it-token@test.ee");
        var tampered = _factory.CreateClient();
        tampered.DefaultRequestHeaders.Authorization = new("Bearer", auth.Token[..^3] + "abc");

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await tampered.GetAsync("/api/auth/me")).StatusCode);
    }
}

internal static class HttpClientExtensions
{
    public static Task<HttpResponseMessage> PatchAsJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PatchAsync(url, JsonContent.Create(body, options: ApiFactory.Json));
}
