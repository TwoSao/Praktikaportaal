using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Web.Services;

public class ApiClient : IApiClient
{
    /// <summary>Same JSON conventions as the API: camelCase, enums as strings.</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;

    public ApiClient(HttpClient http)
    {
        _http = http;
    }

    // ── auth ────────────────────────────────────────────────────────────────

    public Task<AuthResultDto> LoginAsync(LoginDto dto, CancellationToken ct = default) =>
        PostAsync<AuthResultDto>("api/auth/login", dto, ct);

    public Task<AuthResultDto> RegisterStudentAsync(RegisterStudentDto dto, CancellationToken ct = default) =>
        PostAsync<AuthResultDto>("api/auth/register-student", dto, ct);

    public Task<AuthResultDto> RegisterCompanyAsync(RegisterCompanyDto dto, CancellationToken ct = default) =>
        PostAsync<AuthResultDto>("api/auth/register-company", dto, ct);

    // ── internships ─────────────────────────────────────────────────────────

    public Task<PagedResult<InternshipDto>> SearchInternshipsAsync(InternshipQueryParameters query, CancellationToken ct = default) =>
        GetAsync<PagedResult<InternshipDto>>("api/internships" + QueryStringBuilder.Build(query), ct);

    public Task<InternshipFiltersDto> GetInternshipFiltersAsync(CancellationToken ct = default) =>
        GetAsync<InternshipFiltersDto>("api/internships/filters", ct);

    public Task<InternshipDto> GetInternshipAsync(Guid id, CancellationToken ct = default) =>
        GetAsync<InternshipDto>($"api/internships/{id}", ct);

    public async Task<IReadOnlyList<InternshipDto>> GetMyInternshipsAsync(CancellationToken ct = default) =>
        await GetAsync<List<InternshipDto>>("api/internships/my", ct);

    public Task<InternshipDto> CreateInternshipAsync(CreateInternshipDto dto, CancellationToken ct = default) =>
        PostAsync<InternshipDto>("api/internships", dto, ct);

    public Task<InternshipDto> UpdateInternshipAsync(Guid id, UpdateInternshipDto dto, CancellationToken ct = default) =>
        SendAsync<InternshipDto>(HttpMethod.Put, $"api/internships/{id}", dto, ct);

    public Task DeleteInternshipAsync(Guid id, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Delete, $"api/internships/{id}", null, ct);

    public Task SetInternshipStatusAsync(Guid id, InternshipStatus status, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Patch, $"api/internships/{id}/status", new UpdateInternshipStatusDto { Status = status }, ct);

    // ── applications ────────────────────────────────────────────────────────

    public Task<ApplicationDto> ApplyAsync(Guid internshipId, CreateApplicationDto dto, CancellationToken ct = default) =>
        PostAsync<ApplicationDto>($"api/internships/{internshipId}/applications", dto, ct);

    public Task<PagedResult<ApplicationDto>> GetMyApplicationsAsync(ApplicationQueryParameters query, CancellationToken ct = default) =>
        GetAsync<PagedResult<ApplicationDto>>("api/applications/my" + QueryStringBuilder.Build(query), ct);

    public Task<PagedResult<ApplicationDto>> GetInternshipApplicationsAsync(Guid internshipId, ApplicationQueryParameters query, CancellationToken ct = default) =>
        GetAsync<PagedResult<ApplicationDto>>($"api/internships/{internshipId}/applications" + QueryStringBuilder.Build(query), ct);

    public Task SetApplicationStatusAsync(Guid applicationId, ApplicationStatus status, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Patch, $"api/applications/{applicationId}/status", new UpdateApplicationStatusDto { Status = status }, ct);

    public Task WithdrawApplicationAsync(Guid applicationId, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, $"api/applications/{applicationId}/withdraw", null, ct);

    public Task<ApiFile> DownloadApplicantCvAsync(Guid applicationId, CancellationToken ct = default) =>
        DownloadAsync($"api/applications/{applicationId}/cv", ct);

    // ── student profile, CV, recommendations ────────────────────────────────

    public Task<StudentProfileDto> GetMyProfileAsync(CancellationToken ct = default) =>
        GetAsync<StudentProfileDto>("api/students/me", ct);

    public Task<StudentProfileDto> UpdateMyProfileAsync(UpdateStudentProfileDto dto, CancellationToken ct = default) =>
        SendAsync<StudentProfileDto>(HttpMethod.Put, "api/students/me", dto, ct);

    public async Task<CvDto?> GetMyCvAsync(CancellationToken ct = default)
    {
        try
        {
            return await GetAsync<CvDto>("api/students/me/cv", ct);
        }
        catch (ApiException ex) when (ex.IsNotFound)
        {
            return null; // no CV uploaded yet
        }
    }

    public async Task<CvDto> UploadMyCvAsync(Stream content, string fileName, string? contentType, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        var file = new StreamContent(content);
        if (!string.IsNullOrWhiteSpace(contentType) && MediaTypeHeaderValue.TryParse(contentType, out var mediaType))
            file.Headers.ContentType = mediaType;
        form.Add(file, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/students/me/cv") { Content = form };
        return await ReadAsync<CvDto>(request, ct);
    }

    public Task DeleteMyCvAsync(CancellationToken ct = default) =>
        SendAsync(HttpMethod.Delete, "api/students/me/cv", null, ct);

    public Task<ApiFile> DownloadMyCvAsync(CancellationToken ct = default) =>
        DownloadAsync("api/students/me/cv/file", ct);

    public Task<PagedResult<RecommendationDto>> GetRecommendationsAsync(Guid studentProfileId, RecommendationQueryParameters query, CancellationToken ct = default) =>
        GetAsync<PagedResult<RecommendationDto>>($"api/students/{studentProfileId}/recommendations" + QueryStringBuilder.Build(query), ct);

    // ── skills ──────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SkillDto>> GetSkillsAsync(CancellationToken ct = default) =>
        await GetAsync<List<SkillDto>>("api/skills", ct);

    // ── notifications ───────────────────────────────────────────────────────

    public Task<NotificationListDto> GetNotificationsAsync(NotificationQueryParameters query, CancellationToken ct = default) =>
        GetAsync<NotificationListDto>("api/notifications/my" + QueryStringBuilder.Build(query), ct);

    public Task MarkNotificationReadAsync(Guid id, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Patch, $"api/notifications/{id}/read", null, ct);

    public Task MarkAllNotificationsReadAsync(CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, "api/notifications/read-all", null, ct);

    // ── admin ───────────────────────────────────────────────────────────────

    public Task<ApplicationStatisticsDto> GetStatisticsAsync(StatisticsQueryParameters query, CancellationToken ct = default) =>
        GetAsync<ApplicationStatisticsDto>("api/admin/statistics" + QueryStringBuilder.Build(query), ct);

    // ── plumbing ────────────────────────────────────────────────────────────

    private Task<T> GetAsync<T>(string url, CancellationToken ct) => SendAsync<T>(HttpMethod.Get, url, null, ct);

    private Task<T> PostAsync<T>(string url, object body, CancellationToken ct) => SendAsync<T>(HttpMethod.Post, url, body, ct);

    private async Task<T> SendAsync<T>(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var request = CreateRequest(method, url, body);
        return await ReadAsync<T>(request, ct);
    }

    private async Task SendAsync(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var request = CreateRequest(method, url, body);
        using var response = await ExecuteAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, object? body) =>
        new(method, url) { Content = body == null ? null : JsonContent.Create(body, body.GetType(), options: Json) };

    private async Task<T> ReadAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await ExecuteAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<T>(Json, ct)
               ?? throw new ApiException(response.StatusCode, "Tühi vastus", "Server tagastas tühja vastuse.");
    }

    private async Task<ApiFile> DownloadAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await ExecuteAsync(request, ct);
        await EnsureSuccessAsync(response, ct);

        string fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                          ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                          ?? "cv";
        return new ApiFile(
            await response.Content.ReadAsByteArrayAsync(ct),
            fileName,
            response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream");
    }

    private async Task<HttpResponseMessage> ExecuteAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            return await _http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiUnavailableException(ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new ApiUnavailableException(ex); // timeout
        }
    }

    /// <summary>Turns an error response (RFC 7807 ProblemDetails) into an <see cref="ApiException"/>.</summary>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        string title = response.ReasonPhrase ?? "Viga";
        string? detail = null;
        Dictionary<string, string[]>? errors = null;

        try
        {
            string body = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String) title = t.GetString()!;
                    if (root.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String) detail = d.GetString();
                    if (root.TryGetProperty("errors", out var e) && e.ValueKind == JsonValueKind.Object)
                        errors = e.EnumerateObject().ToDictionary(
                            p => p.Name,
                            p => p.Value.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray());
                }
            }
        }
        catch (JsonException)
        {
            // not JSON (e.g. a proxy error page): keep the generic title
        }

        throw new ApiException(response.StatusCode, title, detail, errors);
    }
}
