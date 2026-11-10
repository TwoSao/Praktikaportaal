using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Praktikaportaal.Worker;
using Xunit;

namespace Praktikaportaal.Tests.WorkerTests;

public class ScheduleTests
{
    private static readonly TimeOnly Seven = new(7, 0);

    [Theory]
    [InlineData("2026-11-10 06:59:00", "2026-11-10 07:00:00")]  // before 07:00 -> today
    [InlineData("2026-11-10 07:00:00", "2026-11-11 07:00:00")]  // exactly 07:00 -> tomorrow (already running)
    [InlineData("2026-11-10 07:00:01", "2026-11-11 07:00:00")]
    [InlineData("2026-11-10 23:59:59", "2026-11-11 07:00:00")]
    [InlineData("2026-11-10 00:00:00", "2026-11-10 07:00:00")]
    [InlineData("2026-12-31 12:00:00", "2027-01-01 07:00:00")]  // year boundary
    public void NextRun_IsTheNextSevenAm(string now, string expected) =>
        Assert.Equal(DateTime.Parse(expected), Schedule.NextRun(DateTime.Parse(now), Seven));

    [Fact]
    public void DelayUntilNextRun_IsTheDifference() =>
        Assert.Equal(TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(30)),
                     Schedule.DelayUntilNextRun(DateTime.Parse("2026-11-10 04:30:00"), Seven));

    [Fact]
    public void CustomRunTime_IsRespected() =>
        Assert.Equal(DateTime.Parse("2026-11-10 18:30:00"), Schedule.NextRun(DateTime.Parse("2026-11-10 09:00:00"), new TimeOnly(18, 30)));
}

public class WorkerOptionsTests
{
    private static WorkerOptions Valid() => new() { ServiceEmail = "service@x.ee", ServicePassword = "secret" };

    [Fact]
    public void ValidOptions_HaveNoProblem() => Assert.Null(Valid().Validate());

    [Fact]
    public void MissingCredentials_AreReported()
    {
        var options = Valid();
        options.ServicePassword = "";

        Assert.Contains("ServicePassword", options.Validate());
    }

    [Theory]
    [InlineData("7:00")]
    [InlineData("25:00")]
    [InlineData("seven")]
    public void BadRunAt_IsReported(string runAt)
    {
        var options = Valid();
        options.RunAt = runAt;

        Assert.Contains("RunAt", options.Validate());
    }

    [Fact]
    public void PageSizeAboveTheApiLimit_IsReported()
    {
        var options = Valid();
        options.PageSize = 500;

        Assert.Contains("PageSize", options.Validate());
    }

    [Fact]
    public void RunAtTime_ParsesTheConfiguredValue()
    {
        var options = Valid();
        options.RunAt = "06:45";

        Assert.Equal(new TimeOnly(6, 45), options.RunAtTime);
    }
}

public class LastRunStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "praktikaportaal-worker-" + Guid.NewGuid().ToString("N"));

    private JsonFileLastRunStore Create(string file = "state.json") =>
        new(Options.Create(new WorkerOptions { StateFilePath = Path.Combine(_dir, file) }), NullLogger<JsonFileLastRunStore>.Instance);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task Load_WithoutFile_IsNull() => Assert.Null(await Create().LoadAsync(CancellationToken.None));

    [Fact]
    public async Task SaveThenLoad_RoundTrips_AsUtc()
    {
        var store = Create();
        var time = new DateTime(2026, 11, 10, 7, 0, 5, DateTimeKind.Utc);

        await store.SaveAsync(time, CancellationToken.None);
        var loaded = await Create().LoadAsync(CancellationToken.None); // a new instance reads the file

        Assert.Equal(time, loaded);
        Assert.Equal(DateTimeKind.Utc, loaded!.Value.Kind);
    }

    [Fact]
    public async Task Save_Twice_OverwritesWithoutLeavingTempFiles()
    {
        var store = Create();
        await store.SaveAsync(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None);
        await store.SaveAsync(new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None);

        Assert.Equal(new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc), await store.LoadAsync(CancellationToken.None));
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public async Task CorruptFile_IsIgnored()
    {
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(Path.Combine(_dir, "state.json"), "{ this is not json");

        Assert.Null(await Create().LoadAsync(CancellationToken.None));
    }
}

public class DigestJobTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 7, 0, 0, TimeSpan.Zero);

    private readonly Mock<IServiceApiClient> _api = new();
    private readonly Mock<ILastRunStore> _store = new();
    private readonly WorkerOptions _options = new() { ServiceEmail = "s@x.ee", ServicePassword = "p", PageSize = 2, MaxItemsInDigest = 2 };

    private DigestJob CreateJob() => new(_api.Object, _store.Object, Options.Create(_options), new FixedTime(Now), NullLogger<DigestJob>.Instance);

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static StudentSummary Student(string name) => new(Guid.NewGuid(), Guid.NewGuid(), name, "Student", name.ToLower() + "@student.ee");

    private static Recommendation Match(string title, double percent) =>
        new(percent, new InternshipSummary(Guid.NewGuid(), title, "Acme OÜ", "Tallinn"), ["C#"]);

    private void NewInternships(int count) =>
        _api.Setup(a => a.CountNewInternshipsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(count);

    private void Students(params StudentSummary[] students) =>
        _api.Setup(a => a.GetStudentsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int page, int size, CancellationToken _) =>
                new PageOf<StudentSummary>(students.Skip((page - 1) * size).Take(size).ToList(), students.Length));

    private void Recommendations(StudentSummary student, params Recommendation[] matches) =>
        _api.Setup(a => a.GetRecommendationsAsync(student.Id, It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageOf<Recommendation>(matches.Take(_options.MaxItemsInDigest).ToList(), matches.Length));

    [Fact]
    public async Task OnlyStudentsWithMatches_GetADigest_AndLastRunAdvances()
    {
        var mari = Student("Mari"); var jaan = Student("Jaan");
        _store.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new DateTime(2026, 11, 9, 7, 0, 0, DateTimeKind.Utc));
        NewInternships(3);
        Students(mari, jaan);
        Recommendations(mari, Match("Backend Intern", 100), Match("DevOps Intern", 50), Match("QA Intern", 33.3));
        Recommendations(jaan); // no matches

        var result = await CreateJob().RunAsync(CancellationToken.None);

        Assert.Equal(new DigestResult(3, 2, 1, 0), result);
        _api.Verify(a => a.CreateNotificationAsync(
            mari.UserId,
            "3 new internships match your skills",
            "Backend Intern at Acme OÜ (100 %); DevOps Intern at Acme OÜ (50 %); and 1 more",
            It.IsAny<CancellationToken>()), Times.Once);
        _api.Verify(a => a.CreateNotificationAsync(jaan.UserId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _store.Verify(s => s.SaveAsync(Now.UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecommendationsAreRequested_SinceTheLastRun()
    {
        var student = Student("Mari");
        var lastRun = new DateTime(2026, 11, 9, 7, 0, 0, DateTimeKind.Utc);
        _store.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(lastRun);
        NewInternships(1);
        Students(student);
        Recommendations(student);

        await CreateJob().RunAsync(CancellationToken.None);

        _api.Verify(a => a.CountNewInternshipsAsync(lastRun, It.IsAny<CancellationToken>()), Times.Once);
        _api.Verify(a => a.GetRecommendationsAsync(student.Id, lastRun, 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FirstRun_LooksBackOneDay()
    {
        _store.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync((DateTime?)null);
        NewInternships(0);

        await CreateJob().RunAsync(CancellationToken.None);

        _api.Verify(a => a.CountNewInternshipsAsync(Now.UtcDateTime.AddDays(-1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NoNewInternships_SkipsStudentsButStillAdvancesLastRun()
    {
        _store.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync((DateTime?)null);
        NewInternships(0);

        var result = await CreateJob().RunAsync(CancellationToken.None);

        Assert.Equal(new DigestResult(0, 0, 0, 0), result);
        _api.Verify(a => a.GetStudentsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _store.Verify(s => s.SaveAsync(Now.UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StudentsAreReadPageByPage()
    {
        var all = Enumerable.Range(1, 5).Select(i => Student("S" + i)).ToArray();
        _store.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync((DateTime?)null);
        NewInternships(1);
        Students(all);
        foreach (var s in all) Recommendations(s);

        var result = await CreateJob().RunAsync(CancellationToken.None);

        Assert.Equal(5, result.StudentsChecked);
        _api.Verify(a => a.GetStudentsAsync(It.IsAny<int>(), 2, It.IsAny<CancellationToken>()), Times.Exactly(3)); // 2 + 2 + 1
    }

    [Fact]
    public async Task OneStudentFailing_DoesNotStopTheOthers()
    {
        var broken = Student("Broken"); var healthy = Student("Healthy");
        _store.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync((DateTime?)null);
        NewInternships(1);
        Students(broken, healthy);
        _api.Setup(a => a.GetRecommendationsAsync(broken.Id, It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("boom"));
        Recommendations(healthy, Match("Backend Intern", 100));

        var result = await CreateJob().RunAsync(CancellationToken.None);

        Assert.Equal(new DigestResult(1, 2, 1, 1), result);
        _api.Verify(a => a.CreateNotificationAsync(healthy.UserId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _store.Verify(s => s.SaveAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WholeRunFailing_DoesNotAdvanceLastRun_SoTheNextRunCoversTheSamePeriod()
    {
        _store.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync((DateTime?)null);
        _api.Setup(a => a.CountNewInternshipsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service account login failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateJob().RunAsync(CancellationToken.None));

        _store.Verify(s => s.SaveAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StudentListFailing_DoesNotAdvanceLastRun()
    {
        _store.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync((DateTime?)null);
        NewInternships(2);
        _api.Setup(a => a.GetStudentsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API down"));

        await Assert.ThrowsAsync<HttpRequestException>(() => CreateJob().RunAsync(CancellationToken.None));

        _store.Verify(s => s.SaveAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Title_UsesSingularForOne()
    {
        Assert.Equal("1 new internship matches your skills", DigestJob.BuildTitle(1));
        Assert.Equal("4 new internships match your skills", DigestJob.BuildTitle(4));
    }

    [Fact]
    public void Message_IsCappedAtTheApiLimit()
    {
        var items = Enumerable.Range(1, 50).Select(i => Match(new string('x', 60) + i, 50)).ToList();

        var message = DigestJob.BuildMessage(new PageOf<Recommendation>(items, 80));

        Assert.True(message.Length <= 1000);
        Assert.EndsWith("...", message);
    }
}

/// <summary>The real HTTP client against a scripted fake server.</summary>
public class ServiceApiClientTests
{
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _respond;
        public List<(string Method, string Path, string? Auth)> Calls { get; } = [];

        public ScriptedHandler(Func<HttpRequestMessage, int, HttpResponseMessage> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls.Add((request.Method.Method, request.RequestUri!.PathAndQuery, request.Headers.Authorization?.ToString()));
            return Task.FromResult(_respond(request, Calls.Count));
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static string LoginJson(string token) => JsonSerializer.Serialize(new { token });

    private static ServiceApiClient Create(ScriptedHandler handler, int maxAttempts = 3) => new(
        new HttpClient(handler) { BaseAddress = new Uri("http://api.test/") },
        Options.Create(new WorkerOptions { ServiceEmail = "s@x.ee", ServicePassword = "p", MaxAttempts = maxAttempts, RetryDelaySeconds = 0 }),
        NullLogger<ServiceApiClient>.Instance);

    [Fact]
    public async Task FirstCall_LogsIn_ThenSendsTheBearerToken_AndReusesIt()
    {
        var handler = new ScriptedHandler((req, _) => req.RequestUri!.AbsolutePath == "/api/auth/login"
            ? Json(HttpStatusCode.OK, LoginJson("tok1"))
            : Json(HttpStatusCode.OK, """{"items":[],"totalCount":7}"""));
        var client = Create(handler);

        int first = await client.CountNewInternshipsAsync(new DateTime(2026, 11, 9, 7, 0, 0, DateTimeKind.Utc), CancellationToken.None);
        await client.CountNewInternshipsAsync(DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(7, first);
        Assert.Equal(3, handler.Calls.Count);                 // login, request, request (no second login)
        Assert.Equal("POST", handler.Calls[0].Method);
        Assert.Equal("Bearer tok1", handler.Calls[1].Auth);
        Assert.Contains("createdAfter=2026-11-09T07%3A00%3A00.0000000Z", handler.Calls[1].Path);
        Assert.Contains("pageSize=1", handler.Calls[1].Path);
    }

    [Fact]
    public async Task Unauthorized_TriggersOneReLoginAndRetry()
    {
        int logins = 0;
        var handler = new ScriptedHandler((req, _) =>
        {
            if (req.RequestUri!.AbsolutePath == "/api/auth/login")
                return Json(HttpStatusCode.OK, LoginJson("token-" + (++logins)));
            // the first token is "expired"
            return req.Headers.Authorization!.Parameter == "token-1"
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : Json(HttpStatusCode.OK, """{"items":[],"totalCount":2}""");
        });

        var result = await Create(handler).GetStudentsAsync(1, 50, CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, logins);
    }

    [Fact]
    public async Task PersistentUnauthorized_Fails_InsteadOfLoopingForever()
    {
        var handler = new ScriptedHandler((req, _) => req.RequestUri!.AbsolutePath == "/api/auth/login"
            ? Json(HttpStatusCode.OK, LoginJson("t"))
            : new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => Create(handler).GetStudentsAsync(1, 50, CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Equal(4, handler.Calls.Count); // login, request(401), login, request(401)
    }

    [Fact]
    public async Task ServerErrors_AreRetried_ThenSucceed()
    {
        int requests = 0;
        var handler = new ScriptedHandler((req, _) =>
        {
            if (req.RequestUri!.AbsolutePath == "/api/auth/login") return Json(HttpStatusCode.OK, LoginJson("t"));
            return ++requests < 3
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : Json(HttpStatusCode.OK, """{"items":[],"totalCount":0}""");
        });

        await Create(handler).GetStudentsAsync(1, 50, CancellationToken.None);

        Assert.Equal(3, requests);
    }

    [Fact]
    public async Task ServerErrors_GiveUpAfterMaxAttempts()
    {
        var handler = new ScriptedHandler((req, _) => req.RequestUri!.AbsolutePath == "/api/auth/login"
            ? Json(HttpStatusCode.OK, LoginJson("t"))
            : new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => Create(handler, maxAttempts: 2).GetStudentsAsync(1, 50, CancellationToken.None));

        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
        Assert.Equal(1 + 2, handler.Calls.Count); // login + 2 attempts
    }

    [Fact]
    public async Task ClientErrors_AreNotRetried()
    {
        var handler = new ScriptedHandler((req, _) => req.RequestUri!.AbsolutePath == "/api/auth/login"
            ? Json(HttpStatusCode.OK, LoginJson("t"))
            : Json(HttpStatusCode.BadRequest, """{"title":"bad"}"""));

        await Assert.ThrowsAsync<HttpRequestException>(() => Create(handler).GetStudentsAsync(1, 50, CancellationToken.None));

        Assert.Equal(2, handler.Calls.Count);
    }

    [Fact]
    public async Task WrongServiceCredentials_GiveAClearError()
    {
        var handler = new ScriptedHandler((_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Create(handler).GetStudentsAsync(1, 50, CancellationToken.None));

        Assert.Contains("Service account login failed", ex.Message);
        Assert.Contains("401", ex.Message);
    }

    [Fact]
    public async Task CreateNotification_PostsTheDigestAsJson()
    {
        string? body = null;
        var handler = new ScriptedHandler((req, _) =>
        {
            if (req.RequestUri!.AbsolutePath == "/api/auth/login") return Json(HttpStatusCode.OK, LoginJson("t"));
            body = req.Content!.ReadAsStringAsync().Result;
            return Json(HttpStatusCode.Created, "{}");
        });
        var userId = Guid.NewGuid();

        await Create(handler).CreateNotificationAsync(userId, "Title", "Message", CancellationToken.None);

        using var json = JsonDocument.Parse(body!);
        Assert.Equal(userId, json.RootElement.GetProperty("userId").GetGuid());
        Assert.Equal("Title", json.RootElement.GetProperty("title").GetString());
        Assert.Equal("Message", json.RootElement.GetProperty("message").GetString());
        Assert.Equal("/api/notifications", handler.Calls[1].Path);
    }
}
