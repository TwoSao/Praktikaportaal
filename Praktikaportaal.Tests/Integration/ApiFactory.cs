using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Core.Interfaces;
using Praktikaportaal.Infrastructure.Persistence;

namespace Praktikaportaal.Tests.Integration;

/// <summary>
/// Runs the real API (routing, auth, validation, ProblemDetails, WebSockets) in memory:
/// EF Core InMemory database, throw-away file storage, no seeding and no SQL Server.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly string _filesRoot = Path.Combine(Path.GetTempPath(), "praktikaportaal-it-" + Guid.NewGuid().ToString("N"));

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing"); // do not load appsettings.Development.json (SQL Server, seed)

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "Integration_Test_Secret_Key_That_Is_Long_Enough_32+",
            ["Jwt:Issuer"] = "Praktikaportaal",
            ["Jwt:Audience"] = "PraktikaportaalApp",
            ["Database:MigrateAndSeedOnStartup"] = "false",
            ["FileStorage:RootPath"] = _filesRoot,
            ["Serilog:WriteTo:1:Args:path"] = Path.Combine(_filesRoot, "logs", "api-.log")
        }));

        builder.ConfigureServices(services =>
            services.AddDbContext<PraktikaportaalDbContext>(o => o.UseInMemoryDatabase(_databaseName)));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_filesRoot))
        {
            try { Directory.Delete(_filesRoot, recursive: true); } catch (IOException) { /* log file still open */ }
        }
    }

    // ── helpers used by the tests ───────────────────────────────────────────

    public async Task<(HttpClient Client, AuthResultDto Auth)> RegisterStudentAsync(string email, string password = "Passw0rd!")
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register-student", new RegisterStudentDto
        {
            Email = email, Password = password, FirstName = "Test", LastName = "Student",
            SchoolName = "TTHK", StudyProgram = "IT", Course = 2
        }, Json);
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResultDto>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return (client, auth);
    }

    public async Task<(HttpClient Client, AuthResultDto Auth)> RegisterCompanyAsync(string email, string name = "Test OÜ", string password = "Passw0rd!")
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register-company", new RegisterCompanyDto
        {
            Email = email, Password = password, FirstName = "Hr", LastName = "Person",
            CompanyName = name, RegistrationCode = Guid.NewGuid().ToString("N")[..8]
        }, Json);
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResultDto>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return (client, auth);
    }

    /// <summary>Creates an Admin or Service user directly in the database (they cannot self-register) and logs in.</summary>
    public async Task<HttpClient> LoginAsPrivilegedUserAsync(UserRole role, string email, string password = "Passw0rd!")
    {
        using (var scope = Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PraktikaportaalDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            context.Users.Add(new User
            {
                Email = email, FirstName = role.ToString(), LastName = "User", Role = role,
                PasswordHash = hasher.HashPassword(password)
            });
            await context.SaveChangesAsync();
        }

        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = email, Password = password }, Json);
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResultDto>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    public static async Task<InternshipDto> CreateInternshipAsync(HttpClient companyClient, string title = "Backend Intern", string field = "IT")
    {
        var response = await companyClient.PostAsJsonAsync("/api/internships", new CreateInternshipDto
        {
            Title = title, Description = "A long enough description.", Field = field, Location = "Tallinn"
        }, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InternshipDto>(Json))!;
    }
}
