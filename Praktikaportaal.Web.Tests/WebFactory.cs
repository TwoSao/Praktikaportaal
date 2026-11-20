using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Web.Services;

namespace Praktikaportaal.Web.Tests;

/// <summary>
/// The real MVC site (routing, filters, Razor views, cookie login, antiforgery) with a mocked
/// <see cref="IApiClient"/>, so no API or database is needed.
/// </summary>
public class WebFactory : WebApplicationFactory<Program>
{
    public Mock<IApiClient> Api { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IApiClient>();
            services.AddSingleton(Api.Object);
        });
    }

    /// <summary>A browser-like client: keeps cookies, does not follow redirects automatically.</summary>
    public HttpClient CreateBrowser() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    // ── helpers ─────────────────────────────────────────────────────────────

    public static string? FindToken(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"") is { Success: true } m ? m.Groups[1].Value : null;

    /// <summary>GETs the form page for its antiforgery token (and cookie), then POSTs the form.</summary>
    public static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client, string path, Dictionary<string, string> fields, string? formPage = null)
    {
        string html = await client.GetStringAsync(formPage ?? path);
        var data = new Dictionary<string, string>(fields);
        if (FindToken(html) is { } token)
            data["__RequestVerificationToken"] = token;
        return await client.PostAsync(path, new FormUrlEncodedContent(data));
    }

    public static async Task<string> BodyAsync(HttpResponseMessage response) =>
        WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

    /// <summary>Logs the browser in through the real login form; the mocked API answers with <paramref name="result"/>.</summary>
    public async Task LoginAsync(HttpClient client, UserRole role, Guid? profileId = null, string firstName = "Test", string lastName = "User")
    {
        Api.Setup(a => a.LoginAsync(It.IsAny<LoginDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result(role, profileId, firstName, lastName));
        var response = await PostFormAsync(client, "/Account/Login", new() { ["Email"] = "user@test.ee", ["Password"] = "Passw0rd1" });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    public static AuthResultDto Result(UserRole role, Guid? profileId = null, string firstName = "Test", string lastName = "User") => new()
    {
        Token = FakeJwt(DateTimeOffset.UtcNow.AddHours(2)),
        User = new UserDto { Id = Guid.NewGuid(), Email = "user@test.ee", FirstName = firstName, LastName = lastName, Role = role, ProfileId = profileId ?? Guid.NewGuid() }
    };

    /// <summary>A JWT-shaped token (unsigned) with an exp claim; the site only reads exp.</summary>
    public static string FakeJwt(DateTimeOffset expires)
    {
        static string B64(string s) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{B64("{\"alg\":\"none\"}")}.{B64($"{{\"exp\":{expires.ToUnixTimeSeconds()}}}")}.sig";
    }

    public static InternshipDto Internship(string title = "Backend Intern", string company = "Acme OÜ", string field = "IT") => new()
    {
        Id = Guid.NewGuid(), CompanyProfileId = Guid.NewGuid(), CompanyName = company, Title = title, Description = "A description of the internship.",
        Field = field, Location = "Tallinn", Status = InternshipStatus.Published, CreatedAt = DateTime.UtcNow, Skills = ["C#", "SQL"],
        ApplicationDeadline = DateTime.UtcNow.AddDays(10)
    };

    public static PagedResult<T> Page<T>(IEnumerable<T> items, int total, int page = 1, int pageSize = 9) =>
        new() { Items = items.ToList(), TotalCount = total, Page = page, PageSize = pageSize };
}
