using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Http;
using Moq;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Web.Services;

namespace Praktikaportaal.Web.Tests;

public class QueryStringBuilderTests
{
    [Fact]
    public void Build_SkipsNullAndBlankValues_AndUsesCamelCase()
    {
        var query = new InternshipQueryParameters { Search = "  dev ", Field = "", Location = null, IsRemote = true, Sort = InternshipSort.Deadline, Page = 2, PageSize = 9 };

        string result = QueryStringBuilder.Build(query);

        Assert.StartsWith("?", result);
        var parts = result[1..].Split('&').ToHashSet();
        Assert.Contains("search=dev", parts);
        Assert.Contains("isRemote=true", parts);
        Assert.Contains("sort=Deadline", parts);
        Assert.Contains("page=2", parts);
        Assert.Contains("pageSize=9", parts);
        Assert.DoesNotContain(parts, p => p.StartsWith("field=") || p.StartsWith("location="));
    }

    [Fact]
    public void Build_EscapesValues()
    {
        string result = QueryStringBuilder.Build(new InternshipQueryParameters { Search = "a&b c=d", Page = 1, PageSize = 1 });

        Assert.Contains("search=a%26b%20c%3Dd", result);
    }

    [Fact]
    public void Build_FormatsDatesAsIsoUtc_AndNumbersInvariantly()
    {
        string result = QueryStringBuilder.Build(new RecommendationQueryParameters
        {
            Since = new DateTime(2026, 11, 10, 7, 0, 0, DateTimeKind.Utc), MinOverlap = 33.5
        });

        Assert.Contains("since=2026-11-10T07%3A00%3A00.0000000Z", result);
        Assert.Contains("minOverlap=33.5", result); // never a decimal comma
    }

    [Fact]
    public void Build_Null_IsEmpty() => Assert.Equal(string.Empty, QueryStringBuilder.Build(null));
}

public class JwtExpiryTests
{
    [Fact]
    public void ReadsExpClaim()
    {
        var expires = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

        Assert.Equal(expires, JwtExpiry.TryGetExpiry(WebFactory.FakeJwt(expires)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    [InlineData("a.b.c")]
    [InlineData("a.e30.c")]            // payload "{}" - no exp
    public void BadTokens_GiveNull(string? token) => Assert.Null(JwtExpiry.TryGetExpiry(token));
}

public class UiTextTests
{
    [Theory]
    [InlineData(ApplicationStatus.Submitted, "Esitatud")]
    [InlineData(ApplicationStatus.Interview, "Intervjuu")]
    [InlineData(ApplicationStatus.Withdrawn, "Tagasi võetud")]
    public void ApplicationStatus_HasEstonianLabel(ApplicationStatus status, string label) => Assert.Equal(label, status.Label());

    [Fact]
    public void EveryEnumValue_HasALabelAndBadge()
    {
        foreach (var s in Enum.GetValues<ApplicationStatus>()) { Assert.NotEqual(s.ToString(), s.Label()); Assert.NotEmpty(s.BadgeClass()); }
        foreach (var s in Enum.GetValues<InternshipStatus>()) { Assert.NotEqual(s.ToString(), s.Label()); Assert.NotEmpty(s.BadgeClass()); }
        foreach (var s in Enum.GetValues<SkillLevel>()) Assert.NotEqual(s.ToString(), s.Label());
        foreach (var s in Enum.GetValues<InternshipSort>()) Assert.NotEqual(s.ToString(), s.Label());
        foreach (var s in Enum.GetValues<StatisticsPeriod>()) Assert.NotEqual(s.ToString(), s.Label());
    }

    [Fact]
    public void Date_FormatsDayMonthYear_AndDashForNull()
    {
        Assert.Equal("10.11.2026", UiText.Date(new DateTime(2026, 11, 10)));
        Assert.Equal("—", UiText.Date(null));
    }

    [Fact]
    public void Ago_IsRelativeForRecentTimes()
    {
        Assert.Equal("just praegu", UiText.Ago(DateTime.UtcNow));
        Assert.Equal("5 min tagasi", UiText.Ago(DateTime.UtcNow.AddMinutes(-5).AddSeconds(-1)));
        Assert.Equal("3 h tagasi", UiText.Ago(DateTime.UtcNow.AddHours(-3).AddMinutes(-1)));
        Assert.Equal("2 p tagasi", UiText.Ago(DateTime.UtcNow.AddDays(-2).AddMinutes(-1)));
    }
}

/// <summary>The HTTP client against a scripted fake API.</summary>
public class ApiClientTests
{
    private sealed class Handler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public List<HttpRequestMessage> Requests { get; } = [];
        public Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(_respond(request));
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json, string mediaType = "application/json") =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, mediaType) };

    private static ApiClient Create(Handler handler) => new(new HttpClient(handler) { BaseAddress = new Uri("http://api.test/") });

    [Fact]
    public async Task Search_SendsQuery_AndReadsPagedResultWithStringEnums()
    {
        var handler = new Handler(_ => Json(HttpStatusCode.OK, """
            {"items":[{"id":"11111111-1111-1111-1111-111111111111","title":"Backend","status":"Published","skills":["C#"]}],"totalCount":12,"page":2,"pageSize":9}
            """));

        var result = await Create(handler).SearchInternshipsAsync(new InternshipQueryParameters { Field = "IT", Page = 2, PageSize = 9 });

        Assert.Equal(12, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(InternshipStatus.Published, result.Items[0].Status);
        Assert.Contains("field=IT", handler.Requests[0].RequestUri!.Query);
        Assert.Contains("page=2", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task ProblemDetails_BecomesApiException_WithDetailAndFieldErrors()
    {
        var handler = new Handler(_ => Json(HttpStatusCode.BadRequest, """
            {"type":"x","title":"One or more validation errors occurred.","status":400,
             "errors":{"Email":["The Email field is not a valid e-mail address."],"Password":["too short","needs a digit"]}}
            """, "application/problem+json"));

        var ex = await Assert.ThrowsAsync<ApiException>(() => Create(handler).LoginAsync(new LoginDto()));

        Assert.True(ex.IsUserError);
        Assert.Equal(["too short", "needs a digit"], ex.Errors["Password"]);
        Assert.Contains("Email", ex.Errors.Keys);
    }

    [Fact]
    public async Task Conflict_CarriesTheApiDetailMessage()
    {
        var handler = new Handler(_ => Json(HttpStatusCode.Conflict,
            """{"title":"Conflict","status":409,"detail":"You already have an active application for this internship."}""", "application/problem+json"));

        var ex = await Assert.ThrowsAsync<ApiException>(() => Create(handler).ApplyAsync(Guid.NewGuid(), new CreateApplicationDto()));

        Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode);
        Assert.Equal("You already have an active application for this internship.", ex.Detail);
        Assert.True(ex.IsUserError);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task EmptyErrorBodies_StillMapToTheStatus(HttpStatusCode status)
    {
        var handler = new Handler(_ => new HttpResponseMessage(status));

        var ex = await Assert.ThrowsAsync<ApiException>(() => Create(handler).GetInternshipAsync(Guid.NewGuid()));

        Assert.Equal(status, ex.StatusCode);
    }

    [Fact]
    public async Task NonJsonErrorBody_DoesNotCrashTheParser()
    {
        var handler = new Handler(_ => Json(HttpStatusCode.BadGateway, "<html>Bad gateway</html>", "text/html"));

        var ex = await Assert.ThrowsAsync<ApiException>(() => Create(handler).GetSkillsAsync());

        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
    }

    [Fact]
    public async Task NetworkFailure_BecomesApiUnavailable()
    {
        var client = new ApiClient(new HttpClient(new ThrowingHandler()) { BaseAddress = new Uri("http://api.test/") });

        await Assert.ThrowsAsync<ApiUnavailableException>(() => client.GetSkillsAsync());
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("connection refused");
    }

    [Fact]
    public async Task GetMyCv_NotFound_IsNull_ButOtherErrorsStillThrow()
    {
        Assert.Null(await Create(new Handler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))).GetMyCvAsync());

        await Assert.ThrowsAsync<ApiException>(() => Create(new Handler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden))).GetMyCvAsync());
    }

    [Fact]
    public async Task Download_ReturnsBytesNameAndType()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("%PDF-1.4"u8.ToArray()) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = "Mari CV.pdf" };

        var file = await Create(new Handler(_ => response)).DownloadMyCvAsync();

        Assert.Equal("%PDF-1.4"u8.ToArray(), file.Content);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("Mari CV.pdf", file.FileName);
    }

    [Fact]
    public async Task Upload_SendsMultipartFileField()
    {
        string? contentType = null;
        string? body = null;
        var handler = new Handler(request =>
        {
            contentType = request.Content!.Headers.ContentType!.MediaType;
            body = request.Content.ReadAsStringAsync().Result;
            return Json(HttpStatusCode.Created, """{"id":"11111111-1111-1111-1111-111111111111","fileName":"cv.pdf","contentType":"application/pdf"}""");
        });

        var cv = await Create(handler).UploadMyCvAsync(new MemoryStream("%PDF-1.4"u8.ToArray()), "cv.pdf", "application/pdf");

        Assert.Equal("cv.pdf", cv.FileName);
        Assert.Equal("multipart/form-data", contentType);
        Assert.Contains("name=file", body!.Replace("\"", ""));
        Assert.Contains("filename=cv.pdf", body.Replace("\"", ""));
    }

    [Fact]
    public async Task StatusChange_SendsPatchWithStatusAsString()
    {
        string? body = null;
        var handler = new Handler(request =>
        {
            body = request.Content!.ReadAsStringAsync().Result;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var id = Guid.NewGuid();

        await Create(handler).SetApplicationStatusAsync(id, ApplicationStatus.Interview);

        Assert.Equal(HttpMethod.Patch, handler.Requests[0].Method);
        Assert.Equal($"/api/applications/{id}/status", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Contains("\"Interview\"", body);
    }
}

public class BearerTokenHandlerTests
{
    private sealed class Capture : HttpMessageHandler
    {
        public AuthenticationHeaderValue? Auth { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Auth = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private static async Task<AuthenticationHeaderValue?> SendAsAsync(System.Security.Claims.ClaimsPrincipal? user)
    {
        var capture = new Capture();
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(a => a.HttpContext).Returns(user == null ? null : new DefaultHttpContext { User = user });
        var handler = new BearerTokenHandler(accessor.Object) { InnerHandler = capture };

        await new HttpClient(handler).GetAsync("http://api.test/x");
        return capture.Auth;
    }

    [Fact]
    public async Task LoggedInUser_TokenIsSentAsBearer()
    {
        var identity = new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim(BearerTokenHandler.TokenClaim, "abc.def.ghi")], "test");

        var auth = await SendAsAsync(new System.Security.Claims.ClaimsPrincipal(identity));

        Assert.Equal("Bearer", auth?.Scheme);
        Assert.Equal("abc.def.ghi", auth?.Parameter);
    }

    [Fact]
    public async Task Anonymous_SendsNoAuthorizationHeader()
    {
        Assert.Null(await SendAsAsync(new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity())));
        Assert.Null(await SendAsAsync(null));
    }
}
