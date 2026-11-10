using System.Net;
using Moq;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Web.Services;

namespace Praktikaportaal.Web.Tests;

/// <summary>Pages, login and error handling of the real MVC site with a mocked API client.</summary>
public class SiteTests : IClassFixture<WebFactory>
{
    private readonly WebFactory _factory;

    public SiteTests(WebFactory factory)
    {
        _factory = factory;
        _factory.Api.Reset(); // xUnit creates a new instance per test; start each one with a clean mock (no setups, no calls)
    }

    // ── public pages ────────────────────────────────────────────────────────

    [Fact]
    public async Task Home_ShowsLatestInternships_FromTheApi()
    {
        var internship = WebFactory.Internship("Rendered From Api");
        _factory.Api.Setup(a => a.SearchInternshipsAsync(It.IsAny<InternshipQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebFactory.Page([internship], 1));
        _factory.Api.Setup(a => a.GetInternshipFiltersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InternshipFiltersDto { Fields = ["IT", "Design"], Locations = ["Tallinn"] });

        var html = await WebFactory.BodyAsync(await _factory.CreateBrowser().GetAsync("/"));

        Assert.Contains("Rendered From Api", html);
        Assert.Contains($"/Internships/Details/{internship.Id}", html);
        Assert.Contains("<option value=\"Design\">", html);
        Assert.Contains("Registreeru õppurina", html); // anonymous visitors see the sign-up cards
    }

    [Fact]
    public async Task Home_StillOpens_WhenTheApiIsDown()
    {
        _factory.Api.Setup(a => a.SearchInternshipsAsync(It.IsAny<InternshipQueryParameters>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiUnavailableException(new HttpRequestException()));

        var response = await _factory.CreateBrowser().GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("server ei vasta", await WebFactory.BodyAsync(response));
    }

    [Fact]
    public async Task Internships_PassFiltersToTheApi_AndRenderResultsWithPagination()
    {
        InternshipQueryParameters? sent = null;
        _factory.Api.Setup(a => a.SearchInternshipsAsync(It.IsAny<InternshipQueryParameters>(), It.IsAny<CancellationToken>()))
            .Callback<InternshipQueryParameters, CancellationToken>((q, _) => sent = q)
            .ReturnsAsync(WebFactory.Page(Enumerable.Range(1, 9).Select(i => WebFactory.Internship("Intern " + i)), total: 25, page: 2));
        _factory.Api.Setup(a => a.GetInternshipFiltersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InternshipFiltersDto { Fields = ["IT"], Locations = ["Tallinn"] });

        var html = await WebFactory.BodyAsync(await _factory.CreateBrowser().GetAsync("/Internships?field=IT&location=Tallinn&sort=Title&page=2&isRemote=true"));

        Assert.Equal("IT", sent!.Field);
        Assert.Equal("Tallinn", sent.Location);
        Assert.Equal(InternshipSort.Title, sent.Sort);
        Assert.Equal(2, sent.Page);
        Assert.Equal(9, sent.PageSize);
        Assert.True(sent.IsRemote);
        Assert.Equal(9, System.Text.RegularExpressions.Regex.Matches(html, "card card-internship").Count);
        Assert.Contains("Leitud <strong>25</strong>", html);
        Assert.Contains("page=3", html);                 // next page link keeps the other filters
        Assert.Contains("field=IT", html);
    }

    [Fact]
    public async Task Internships_EmptyResult_ShowsEmptyState()
    {
        _factory.Api.Setup(a => a.SearchInternshipsAsync(It.IsAny<InternshipQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebFactory.Page(Array.Empty<InternshipDto>(), 0));
        _factory.Api.Setup(a => a.GetInternshipFiltersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new InternshipFiltersDto());

        var html = await WebFactory.BodyAsync(await _factory.CreateBrowser().GetAsync("/Internships?search=nothing"));

        Assert.Contains("Ühtegi praktikakohta ei leitud", html);
        Assert.Contains("Näita kõiki", html);
    }

    [Fact]
    public async Task Details_ShowsTheInternship()
    {
        var internship = WebFactory.Internship("Details Title");
        _factory.Api.Setup(a => a.GetInternshipAsync(internship.Id, It.IsAny<CancellationToken>())).ReturnsAsync(internship);

        var response = await _factory.CreateBrowser().GetAsync($"/Internships/Details/{internship.Id}");
        var html = await WebFactory.BodyAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Details Title", html);
        Assert.Contains("Vajalikud oskused", html);
        Assert.Contains("Registreeru õppurina", html);
    }

    [Fact]
    public async Task ApiNotFound_BecomesAFriendly404Page()
    {
        var id = Guid.NewGuid();
        _factory.Api.Setup(a => a.GetInternshipAsync(id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException(HttpStatusCode.NotFound, "Resource not found", "gone"));

        var response = await _factory.CreateBrowser().GetAsync($"/Internships/Details/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Lehte ei leitud", await WebFactory.BodyAsync(response));
    }

    [Fact]
    public async Task UnknownUrl_ShowsTheFriendly404Page()
    {
        var response = await _factory.CreateBrowser().GetAsync("/this/does/not/exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Lehte ei leitud", await WebFactory.BodyAsync(response));
    }

    [Fact]
    public async Task ApiDown_OnInternalPage_ShowsFriendlyMessage_Not503Stacktrace()
    {
        _factory.Api.Setup(a => a.GetInternshipAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiUnavailableException(new HttpRequestException("refused")));

        var response = await _factory.CreateBrowser().GetAsync($"/Internships/Details/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var html = await WebFactory.BodyAsync(response);
        Assert.Contains("server ei vasta", html);
        Assert.DoesNotContain("HttpRequestException", html);
    }

    // ── login ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_Success_SetsCookie_AndRedirectsByRole()
    {
        var client = _factory.CreateBrowser();
        _factory.Api.Setup(a => a.LoginAsync(It.IsAny<LoginDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebFactory.Result(UserRole.Company));

        var response = await WebFactory.PostFormAsync(client, "/Account/Login", new() { ["Email"] = "hr@x.ee", ["Password"] = "Passw0rd1" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Company", response.Headers.Location!.OriginalString);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("Praktikaportaal.Auth=") && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Login_SendsTheEnteredCredentialsToTheApi()
    {
        LoginDto? sent = null;
        _factory.Api.Setup(a => a.LoginAsync(It.IsAny<LoginDto>(), It.IsAny<CancellationToken>()))
            .Callback<LoginDto, CancellationToken>((dto, _) => sent = dto)
            .ReturnsAsync(WebFactory.Result(UserRole.Student));

        await WebFactory.PostFormAsync(_factory.CreateBrowser(), "/Account/Login", new() { ["Email"] = "mari@student.ee", ["Password"] = "S3cret!pass" });

        Assert.Equal("mari@student.ee", sent!.Email);
        Assert.Equal("S3cret!pass", sent.Password);
    }

    [Fact]
    public async Task Login_WrongPassword_ShowsOneGenericMessage()
    {
        _factory.Api.Setup(a => a.LoginAsync(It.IsAny<LoginDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException(HttpStatusCode.Unauthorized, "Unauthorized", "Invalid email or password."));

        var response = await WebFactory.PostFormAsync(_factory.CreateBrowser(), "/Account/Login", new() { ["Email"] = "a@b.ee", ["Password"] = "x" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await WebFactory.BodyAsync(response);
        Assert.Contains("Vale e-posti aadress või parool", html);
        // (the antiforgery cookie is expected; the login cookie must not be issued)
        Assert.DoesNotContain(response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : [], c => c.StartsWith("Praktikaportaal.Auth="));
    }

    [Fact]
    public async Task Login_EmptyForm_IsValidatedWithoutCallingTheApi()
    {
        var response = await WebFactory.PostFormAsync(_factory.CreateBrowser(), "/Account/Login", new() { ["Email"] = "", ["Password"] = "" });

        var html = await WebFactory.BodyAsync(response);
        Assert.Contains("Sisesta e-posti aadress", html);
        Assert.Contains("Sisesta parool", html);
        _factory.Api.Verify(a => a.LoginAsync(It.IsAny<LoginDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("/Internships?page=2", "/Internships?page=2")]       // local: honoured
    [InlineData("https://evil.example/phish", "/Internships")]        // external: ignored
    [InlineData("//evil.example", "/Internships")]                    // protocol-relative: ignored
    public async Task Login_ReturnUrl_OnlyLocalUrlsAreFollowed(string returnUrl, string expected)
    {
        _factory.Api.Setup(a => a.LoginAsync(It.IsAny<LoginDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebFactory.Result(UserRole.Student));

        var response = await WebFactory.PostFormAsync(_factory.CreateBrowser(), "/Account/Login",
            new() { ["Email"] = "a@b.ee", ["Password"] = "Passw0rd1", ["ReturnUrl"] = returnUrl }, formPage: "/Account/Login");

        Assert.Equal(expected, response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Post_WithoutAntiforgeryToken_IsRejected()
    {
        var response = await _factory.CreateBrowser().PostAsync("/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["Email"] = "a@b.ee", ["Password"] = "x" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _factory.Api.Verify(a => a.LoginAsync(It.IsAny<LoginDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoggedInUser_SeesNameAndRole_AndLogoutClearsIt()
    {
        var client = _factory.CreateBrowser();
        await _factory.LoginAsync(client, UserRole.Student, firstName: "Mari", lastName: "Tamm");
        _factory.Api.Setup(a => a.SearchInternshipsAsync(It.IsAny<InternshipQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebFactory.Page(Array.Empty<InternshipDto>(), 0));
        _factory.Api.Setup(a => a.GetInternshipFiltersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new InternshipFiltersDto());

        var before = await WebFactory.BodyAsync(await client.GetAsync("/"));
        Assert.Contains("Mari Tamm", before);
        Assert.Contains("Logi välja", before);
        Assert.DoesNotContain("Registreeru õppurina", before);

        var logout = await WebFactory.PostFormAsync(client, "/Account/Logout", new(), formPage: "/");
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);

        var after = await WebFactory.BodyAsync(await client.GetAsync("/"));
        Assert.DoesNotContain("Mari Tamm", after);
        Assert.Contains("Logi sisse", after);
    }

    [Fact]
    public async Task ExpiredApiToken_LogsTheUserOut_AndAsksToLogInAgain()
    {
        var client = _factory.CreateBrowser();
        await _factory.LoginAsync(client, UserRole.Student);
        _factory.Api.Setup(a => a.GetInternshipAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException(HttpStatusCode.Unauthorized, "Unauthorized", "token expired"));

        var response = await client.GetAsync($"/Internships/Details/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login", response.Headers.Location!.OriginalString);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("Praktikaportaal.Auth=;") || c.Contains("expires=Thu, 01 Jan 1970"));
    }

    // ── registration ────────────────────────────────────────────────────────

    private static Dictionary<string, string> StudentForm(string email = "new@student.ee") => new()
    {
        ["FirstName"] = "Mari", ["LastName"] = "Tamm", ["Email"] = email, ["Password"] = "Passw0rd1", ["ConfirmPassword"] = "Passw0rd1",
        ["SchoolName"] = "TTHK", ["StudyProgram"] = "IT", ["Course"] = "2"
    };

    [Fact]
    public async Task RegisterStudent_Success_LogsInAndRedirects()
    {
        RegisterStudentDto? sent = null;
        _factory.Api.Setup(a => a.RegisterStudentAsync(It.IsAny<RegisterStudentDto>(), It.IsAny<CancellationToken>()))
            .Callback<RegisterStudentDto, CancellationToken>((dto, _) => sent = dto)
            .ReturnsAsync(WebFactory.Result(UserRole.Student, firstName: "Mari"));

        var response = await WebFactory.PostFormAsync(_factory.CreateBrowser(), "/Account/RegisterStudent", StudentForm());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Internships", response.Headers.Location!.OriginalString);
        Assert.Equal("new@student.ee", sent!.Email);
        Assert.Equal(2, sent.Course);
        Assert.Null(sent.PhoneNumber); // blank optional fields are sent as null, not ""
    }

    [Fact]
    public async Task RegisterStudent_InvalidInput_IsRejectedByTheSiteBeforeCallingTheApi()
    {
        var form = StudentForm("not-an-email");
        form["ConfirmPassword"] = "different";
        form["Course"] = "9";

        var response = await WebFactory.PostFormAsync(_factory.CreateBrowser(), "/Account/RegisterStudent", form);

        var html = await WebFactory.BodyAsync(response);
        Assert.Contains("E-posti aadress ei ole korrektne", html);
        Assert.Contains("Paroolid ei ühti", html);
        Assert.Contains("Kursus peab olema 1 kuni 6", html);
        _factory.Api.Verify(a => a.RegisterStudentAsync(It.IsAny<RegisterStudentDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterStudent_ApiConflict_ShowsTheApiMessageOnTheForm()
    {
        _factory.Api.Setup(a => a.RegisterStudentAsync(It.IsAny<RegisterStudentDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException(HttpStatusCode.Conflict, "Conflict", "User with this email already exists."));

        var response = await WebFactory.PostFormAsync(_factory.CreateBrowser(), "/Account/RegisterStudent", StudentForm("dup@student.ee"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await WebFactory.BodyAsync(response);
        Assert.Contains("User with this email already exists.", html);
        Assert.Contains("value=\"dup@student.ee\"", html); // the entered data is kept
    }

    [Fact]
    public async Task RegisterStudent_ApiFieldErrors_AreShownNextToTheField()
    {
        _factory.Api.Setup(a => a.RegisterStudentAsync(It.IsAny<RegisterStudentDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException(HttpStatusCode.BadRequest, "Validation", null,
                new Dictionary<string, string[]> { ["StudyProgram"] = ["Study program is not allowed by the API."] }));

        var response = await WebFactory.PostFormAsync(_factory.CreateBrowser(), "/Account/RegisterStudent", StudentForm());

        Assert.Contains("Study program is not allowed by the API.", await WebFactory.BodyAsync(response));
    }

    [Fact]
    public async Task RegisterCompany_Success_RedirectsToCompanyArea()
    {
        _factory.Api.Setup(a => a.RegisterCompanyAsync(It.IsAny<RegisterCompanyDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebFactory.Result(UserRole.Company));

        var response = await WebFactory.PostFormAsync(_factory.CreateBrowser(), "/Account/RegisterCompany", new()
        {
            ["FirstName"] = "Hr", ["LastName"] = "Person", ["Email"] = "hr@acme.ee", ["Password"] = "Passw0rd1", ["ConfirmPassword"] = "Passw0rd1",
            ["CompanyName"] = "Acme OÜ", ["RegistrationCode"] = "12345678"
        });

        Assert.Equal("/Company", response.Headers.Location!.OriginalString);
    }
}
