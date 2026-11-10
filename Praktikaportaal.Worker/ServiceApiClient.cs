using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Praktikaportaal.Worker;

/// <summary>What the digest job needs from the API.</summary>
public interface IServiceApiClient
{
    /// <summary>How many published internships were created after <paramref name="sinceUtc"/>.</summary>
    Task<int> CountNewInternshipsAsync(DateTime sinceUtc, CancellationToken ct);

    Task<PageOf<StudentSummary>> GetStudentsAsync(int page, int pageSize, CancellationToken ct);

    /// <summary>Best matching internships for a student, created after <paramref name="sinceUtc"/>.</summary>
    Task<PageOf<Recommendation>> GetRecommendationsAsync(Guid studentProfileId, DateTime sinceUtc, int take, CancellationToken ct);

    Task CreateNotificationAsync(Guid userId, string title, string message, CancellationToken ct);
}

/// <summary>
/// HTTP client for the API using the Service account: logs in with a JWT, re-logs in when the API answers 401,
/// and retries network errors and 5xx responses.
/// </summary>
public class ServiceApiClient : IServiceApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly WorkerOptions _options;
    private readonly ILogger<ServiceApiClient> _logger;
    private string? _token;

    public ServiceApiClient(HttpClient http, IOptions<WorkerOptions> options, ILogger<ServiceApiClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> CountNewInternshipsAsync(DateTime sinceUtc, CancellationToken ct)
    {
        // pageSize=1: only the total count is needed.
        var page = await GetAsync<PageOf<InternshipSummary>>($"api/internships?createdAfter={Iso(sinceUtc)}&pageSize=1", ct);
        return page.TotalCount;
    }

    public Task<PageOf<StudentSummary>> GetStudentsAsync(int page, int pageSize, CancellationToken ct) =>
        GetAsync<PageOf<StudentSummary>>($"api/students?page={page}&pageSize={pageSize}", ct);

    public Task<PageOf<Recommendation>> GetRecommendationsAsync(Guid studentProfileId, DateTime sinceUtc, int take, CancellationToken ct) =>
        GetAsync<PageOf<Recommendation>>($"api/students/{studentProfileId}/recommendations?since={Iso(sinceUtc)}&pageSize={take}", ct);

    public async Task CreateNotificationAsync(Guid userId, string title, string message, CancellationToken ct)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, "api/notifications")
            {
                Content = JsonContent.Create(new CreateNotificationRequest(userId, title, message), options: Json)
            }, ct);
        await EnsureSuccessAsync(response, ct);
    }

    // ── plumbing ────────────────────────────────────────────────────────────

    private static string Iso(DateTime utc) =>
        Uri.EscapeDataString(DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture));

    private async Task<T> GetAsync<T>(string url, CancellationToken ct)
    {
        using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, url), ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<T>(Json, ct))
               ?? throw new InvalidOperationException($"The API returned an empty body for {url}.");
    }

    private async Task LoginAsync(CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync(
            "api/auth/login", new LoginRequest(_options.ServiceEmail, _options.ServicePassword), Json, ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Service account login failed with HTTP {(int)response.StatusCode}. Check Worker:ServiceEmail / Worker:ServicePassword.");

        _token = (await response.Content.ReadFromJsonAsync<LoginResponse>(Json, ct))?.Token
                 ?? throw new InvalidOperationException("The login response did not contain a token.");
        _logger.LogInformation("Logged in to the API as {Email}", _options.ServiceEmail);
    }

    /// <summary>Sends a request with the JWT; logs in first if needed, re-logs in once on 401, retries transient failures.</summary>
    private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> createRequest, CancellationToken ct)
    {
        bool reloggedIn = false;

        for (int attempt = 1; ; attempt++)
        {
            if (_token == null)
                await LoginAsync(ct);

            using var request = createRequest();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, ct);
            }
            catch (HttpRequestException ex) when (attempt < _options.MaxAttempts)
            {
                _logger.LogWarning(ex, "API request failed (attempt {Attempt}/{Max}), retrying", attempt, _options.MaxAttempts);
                await DelayAsync(attempt, ct);
                continue;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized && !reloggedIn)
            {
                _logger.LogInformation("The API rejected the token (401); logging in again");
                response.Dispose();
                _token = null;
                reloggedIn = true;
                attempt--; // a refresh is not a failed attempt
                continue;
            }

            if ((int)response.StatusCode >= 500 && attempt < _options.MaxAttempts)
            {
                _logger.LogWarning("API answered HTTP {Status} (attempt {Attempt}/{Max}), retrying",
                    (int)response.StatusCode, attempt, _options.MaxAttempts);
                response.Dispose();
                await DelayAsync(attempt, ct);
                continue;
            }

            return response;
        }
    }

    private Task DelayAsync(int attempt, CancellationToken ct) =>
        _options.RetryDelaySeconds > 0 ? Task.Delay(TimeSpan.FromSeconds(_options.RetryDelaySeconds * attempt), ct) : Task.CompletedTask;

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        string body = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException(
            $"API request failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}. {Truncate(body, 300)}",
            inner: null, response.StatusCode);
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max] + "…";
}
