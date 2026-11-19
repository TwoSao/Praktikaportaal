using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Praktikaportaal.Web.Models;

namespace Praktikaportaal.Web.Services;

public class ApiClient : IApiClient
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ApiClient(HttpClient http, IHttpContextAccessor httpContextAccessor)
    {
        _http = http;
        _httpContextAccessor = httpContextAccessor;
    }

    private void AddAuthorizationHeader()
    {
        var token = _httpContextAccessor.HttpContext?.User.FindFirst("jwt_token")?.Value;
        if (!string.IsNullOrEmpty(token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<(bool Success, string? Token, string? Error, dynamic? User)> LoginAsync(LoginViewModel model)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/login", new
        {
            email = model.Email,
            password = model.Password
        });

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        bool success = root.GetProperty("success").GetBoolean();
        if (!success)
        {
            string error = root.TryGetProperty("error", out var err) ? err.GetString() ?? "Login failed" : "Login failed";
            return (false, null, error, null);
        }

        string token = root.GetProperty("token").GetString()!;
        var userEl = root.GetProperty("user");

        return (true, token, null, userEl.Clone());
    }

    public async Task<(bool Success, string? Token, string? Error, dynamic? User)> RegisterStudentAsync(RegisterStudentViewModel model)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/register-student", new
        {
            email = model.Email,
            password = model.Password,
            firstName = model.FirstName,
            lastName = model.LastName,
            schoolName = model.SchoolName,
            studyProgram = model.StudyProgram,
            course = model.Course,
            phoneNumber = model.PhoneNumber,
            location = model.Location
        });

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        bool success = root.GetProperty("success").GetBoolean();
        if (!success)
        {
            string error = root.TryGetProperty("error", out var err) ? err.GetString() ?? "Registration failed" : "Registration failed";
            return (false, null, error, null);
        }

        string token = root.GetProperty("token").GetString()!;
        var userEl = root.GetProperty("user");

        return (true, token, null, userEl.Clone());
    }

    public async Task<(bool Success, string? Token, string? Error, dynamic? User)> RegisterCompanyAsync(RegisterCompanyViewModel model)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/register-company", new
        {
            email = model.Email,
            password = model.Password,
            firstName = model.FirstName,
            lastName = model.LastName,
            companyName = model.CompanyName,
            registrationCode = model.RegistrationCode,
            description = model.Description,
            website = model.Website,
            address = model.Address,
            contactEmail = model.ContactEmail,
            contactPhone = model.ContactPhone
        });

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        bool success = root.GetProperty("success").GetBoolean();
        if (!success)
        {
            string error = root.TryGetProperty("error", out var err) ? err.GetString() ?? "Registration failed" : "Registration failed";
            return (false, null, error, null);
        }

        string token = root.GetProperty("token").GetString()!;
        var userEl = root.GetProperty("user");

        return (true, token, null, userEl.Clone());
    }

    public async Task<List<InternshipItemViewModel>> GetPublishedInternshipsAsync(string? search = null, bool? isRemote = null)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search)}");
        if (isRemote.HasValue) query.Add($"isRemote={isRemote.Value}");

        string url = "/api/internships" + (query.Count > 0 ? "?" + string.Join("&", query) : "");

        var response = await _http.GetAsync(url);
        if (!response.IsSuccessStatusCode) return [];

        var list = await response.Content.ReadFromJsonAsync<List<InternshipItemViewModel>>(JsonOptions);
        return list ?? [];
    }

    public async Task<InternshipItemViewModel?> GetInternshipByIdAsync(Guid id)
    {
        var response = await _http.GetAsync($"/api/internships/{id}");
        if (!response.IsSuccessStatusCode) return null;

        return await response.Content.ReadFromJsonAsync<InternshipItemViewModel>(JsonOptions);
    }

    public async Task<List<InternshipItemViewModel>> GetMyInternshipsAsync()
    {
        AddAuthorizationHeader();
        var response = await _http.GetAsync("/api/internships/my");
        if (!response.IsSuccessStatusCode) return [];

        var list = await response.Content.ReadFromJsonAsync<List<InternshipItemViewModel>>(JsonOptions);
        return list ?? [];
    }

    public async Task<(bool Success, string? Error, InternshipItemViewModel? Item)> CreateInternshipAsync(CreateInternshipViewModel model)
    {
        AddAuthorizationHeader();
        var response = await _http.PostAsJsonAsync("/api/internships", new
        {
            title = model.Title,
            description = model.Description,
            requirements = model.Requirements,
            location = model.Location,
            startDate = model.StartDate,
            endDate = model.EndDate,
            applicationDeadline = model.ApplicationDeadline,
            isRemote = model.IsRemote
        });

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            return (false, "Viga kuulutuse loomisel", null);
        }

        var item = await response.Content.ReadFromJsonAsync<InternshipItemViewModel>(JsonOptions);
        return (true, null, item);
    }

    public async Task<bool> UpdateInternshipStatusAsync(Guid id, string status)
    {
        AddAuthorizationHeader();
        // status is an enum string: Published, Closed, etc.
        int statusInt = status switch
        {
            "Draft" => 0,
            "Published" => 1,
            "Closed" => 2,
            "Archived" => 3,
            _ => 1
        };

        var response = await _http.PatchAsJsonAsync($"/api/internships/{id}/status", new { status = statusInt });
        return response.IsSuccessStatusCode;
    }

    public async Task<(bool Success, string? Error)> ApplyAsync(ApplyViewModel model)
    {
        AddAuthorizationHeader();
        var response = await _http.PostAsJsonAsync("/api/applications", new
        {
            internshipId = model.InternshipId,
            coverLetter = model.CoverLetter
        });

        if (!response.IsSuccessStatusCode)
        {
            string errStr = await response.Content.ReadAsStringAsync();
            try
            {
                using var doc = JsonDocument.Parse(errStr);
                if (doc.RootElement.TryGetProperty("error", out var e))
                    return (false, e.GetString());
            }
            catch { }
            return (false, "Kandideerimine ebaõnnestus.");
        }

        return (true, null);
    }

    public async Task<List<ApplicationItemViewModel>> GetMyApplicationsAsync()
    {
        AddAuthorizationHeader();
        var response = await _http.GetAsync("/api/applications/my");
        if (!response.IsSuccessStatusCode) return [];

        var list = await response.Content.ReadFromJsonAsync<List<ApplicationItemViewModel>>(JsonOptions);
        return list ?? [];
    }

    public async Task<List<ApplicationItemViewModel>> GetInternshipApplicationsAsync(Guid internshipId)
    {
        AddAuthorizationHeader();
        var response = await _http.GetAsync($"/api/applications/internship/{internshipId}");
        if (!response.IsSuccessStatusCode) return [];

        var list = await response.Content.ReadFromJsonAsync<List<ApplicationItemViewModel>>(JsonOptions);
        return list ?? [];
    }

    public async Task<bool> UpdateApplicationStatusAsync(Guid applicationId, string status)
    {
        AddAuthorizationHeader();
        int statusInt = status switch
        {
            "Pending" => 0,
            "Accepted" => 1,
            "Rejected" => 2,
            "Withdrawn" => 3,
            _ => 0
        };

        var response = await _http.PatchAsJsonAsync($"/api/applications/{applicationId}/status", new { status = statusInt });
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> WithdrawApplicationAsync(Guid applicationId)
    {
        AddAuthorizationHeader();
        var response = await _http.PostAsync($"/api/applications/{applicationId}/withdraw", null);
        return response.IsSuccessStatusCode;
    }
}

