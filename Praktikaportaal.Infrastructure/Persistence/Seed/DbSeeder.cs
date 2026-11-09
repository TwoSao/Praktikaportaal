using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Core.Interfaces;

namespace Praktikaportaal.Infrastructure.Persistence.Seed;

/// <summary>
/// Seeds demo data: admin, service account, companies, students, skills,
/// internships (with required skills) and applications.
/// Idempotent — does nothing if the admin account already exists.
/// Passwords for the demo accounts come from configuration section "Seed".
/// </summary>
public class DbSeeder
{
    public const string AdminEmail = "admin@praktikaportaal.ee";
    public const string ServiceEmail = "service@praktikaportaal.ee";

    private readonly PraktikaportaalDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DbSeeder> _logger;

    // Fixed seed so every fresh database gets the same demo data.
    private readonly Random _random = new(42);

    public DbSeeder(
        PraktikaportaalDbContext context,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ILogger<DbSeeder> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await _context.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == AdminEmail, ct))
        {
            _logger.LogInformation("Seed data already present, skipping.");
            return;
        }

        string Password(string key) => _configuration[$"Seed:{key}"]
            ?? throw new InvalidOperationException($"Configuration value 'Seed:{key}' is required for seeding.");

        var skills = CreateSkills();
        _context.Skills.AddRange(skills.Values);

        _context.Users.Add(CreateUser(AdminEmail, Password("AdminPassword"), "Admin", "Kasutaja", UserRole.Admin));
        _context.Users.Add(CreateUser(ServiceEmail, Password("ServicePassword"), "Worker", "Service", UserRole.Service));

        var companies = CreateCompanies(Password("CompanyPassword"));
        var students = CreateStudents(Password("StudentPassword"), skills);
        var internships = CreateInternships(companies, skills);
        var applications = CreateApplications(students, internships);

        _context.Users.AddRange(companies.Select(c => c.User));
        _context.Users.AddRange(students.Select(s => s.User));
        _context.Internships.AddRange(internships);
        _context.Applications.AddRange(applications);

        await _context.SaveChangesAsync(ct);

        // SaveChanges stamps CreatedAt = now; spread internships over the last 30 days
        // so sorting and the Worker's ?createdAfter= filter have realistic data.
        for (int i = 0; i < internships.Count; i++)
        {
            var createdAt = DateTime.UtcNow.AddDays(-(internships.Count - i) * 30.0 / internships.Count);
            var id = internships[i].Id;
            await _context.Internships
                .Where(x => x.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, createdAt), ct);
        }

        _logger.LogInformation(
            "Seeded {Skills} skills, {Companies} companies, {Students} students, {Internships} internships, {Applications} applications.",
            skills.Count, companies.Count, students.Count, internships.Count, applications.Count);
    }

    private User CreateUser(string email, string password, string firstName, string lastName, UserRole role) => new()
    {
        Email = email,
        PasswordHash = _passwordHasher.HashPassword(password),
        FirstName = firstName,
        LastName = lastName,
        Role = role,
        IsActive = true
    };

    private static Dictionary<string, Skill> CreateSkills()
    {
        string[] names =
        [
            "C#", ".NET", "ASP.NET Core", "SQL", "JavaScript", "TypeScript", "React", "HTML/CSS",
            "Python", "Java", "Git", "Docker", "Azure", "Linux", "Figma", "UI/UX Design",
            "Adobe Photoshop", "Marketing", "Social Media", "Copywriting", "Excel",
            "Accounting", "Data Analysis", "Project Management", "AutoCAD", "Logistics",
            "Customer Service", "English", "Estonian", "Russian"
        ];
        return names.ToDictionary(n => n, n => new Skill { Name = n });
    }

    private List<CompanyProfile> CreateCompanies(string password)
    {
        (string Name, string Code, string Address, string Website, string First, string Last, string Description)[] data =
        [
            ("Nordic Software OÜ", "14500001", "Tallinn, Ülemiste City", "https://nordicsoftware.example", "Kristjan", "Saar", "Custom business software and cloud solutions."),
            ("Baltic Marketing AS", "14500002", "Tartu, Riia 15", "https://balticmarketing.example", "Liis", "Mägi", "Digital marketing agency for Baltic brands."),
            ("Tallinn FinTech OÜ", "14500003", "Tallinn, Narva mnt 7", "https://tallinnfintech.example", "Andres", "Kuusk", "Payments and banking APIs."),
            ("Pixel Studio OÜ", "14500004", "Pärnu, Rüütli 20", "https://pixelstudio.example", "Kadri", "Lepp", "Product design and branding studio."),
            ("EstLog Logistics AS", "14500005", "Maardu, Vana-Narva mnt 2", "https://estlog.example", "Margus", "Ilves", "Warehousing and international freight.")
        ];

        return data.Select(d =>
        {
            var user = CreateUser(
                $"hr@{d.Website.Replace("https://", "").Replace(".example", "")}.ee",
                password, d.First, d.Last, UserRole.Company);

            var company = new CompanyProfile
            {
                User = user,
                UserId = user.Id,
                CompanyName = d.Name,
                RegistrationCode = d.Code,
                Address = d.Address,
                Website = d.Website,
                Description = d.Description,
                ContactEmail = user.Email
            };
            user.CompanyProfile = company;
            return company;
        }).ToList();
    }

    private List<StudentProfile> CreateStudents(string password, Dictionary<string, Skill> skills)
    {
        (string First, string Last, string School, string Program, int Course, string Location, string[] Skills)[] data =
        [
            ("Mari", "Tamm", "Tallinna Tööstushariduskeskus", "Software Development", 3, "Tallinn", ["C#", ".NET", "SQL", "Git", "HTML/CSS"]),
            ("Jaan", "Kask", "Tallinna Tööstushariduskeskus", "Software Development", 2, "Tallinn", ["JavaScript", "TypeScript", "React", "HTML/CSS", "Git"]),
            ("Anna", "Ivanova", "TalTech", "Business Information Technology", 2, "Tallinn", ["Python", "SQL", "Data Analysis", "Excel"]),
            ("Karl", "Rebane", "Tartu Ülikool", "Computer Science", 3, "Tartu", ["Java", "Python", "Docker", "Linux", "Git"]),
            ("Laura", "Pärn", "EKA", "Graphic Design", 2, "Tallinn", ["Figma", "UI/UX Design", "Adobe Photoshop", "HTML/CSS"]),
            ("Markus", "Org", "Tartu Ülikool", "Marketing", 1, "Tartu", ["Marketing", "Social Media", "Copywriting", "English"]),
            ("Elena", "Smirnova", "TalTech", "Accounting", 3, "Narva", ["Accounting", "Excel", "Russian", "English"]),
            ("Rasmus", "Kivi", "Pärnu Kutsehariduskeskus", "Logistics", 2, "Pärnu", ["Logistics", "Excel", "Customer Service", "Estonian"])
        ];

        return data.Select(d =>
        {
            var user = CreateUser(
                $"{Ascii(d.First)}.{Ascii(d.Last)}@student.ee".ToLowerInvariant(),
                password, d.First, d.Last, UserRole.Student);

            var student = new StudentProfile
            {
                User = user,
                UserId = user.Id,
                SchoolName = d.School,
                StudyProgram = d.Program,
                Course = d.Course,
                Location = d.Location,
                Description = $"{d.Program} student looking for an internship."
            };
            foreach (var name in d.Skills)
            {
                student.StudentSkills.Add(new StudentSkill
                {
                    StudentProfile = student,
                    Skill = skills[name],
                    Level = (SkillLevel)_random.Next(0, 3)
                });
            }
            user.StudentProfile = student;
            return student;
        }).ToList();
    }

    private List<Internship> CreateInternships(List<CompanyProfile> companies, Dictionary<string, Skill> skills)
    {
        // (company index, title, field, location, remote, required skills)
        (int C, string Title, string Field, string Location, bool Remote, string[] Skills)[] data =
        [
            (0, "Junior .NET Developer Intern", "IT", "Tallinn", false, ["C#", ".NET", "SQL", "Git"]),
            (0, "ASP.NET Core Backend Intern", "IT", "Tallinn", true, ["C#", "ASP.NET Core", "SQL"]),
            (0, "Frontend Developer Intern (React)", "IT", "Tallinn", false, ["JavaScript", "TypeScript", "React", "HTML/CSS"]),
            (0, "DevOps Intern", "IT", "Tallinn", false, ["Docker", "Linux", "Azure", "Git"]),
            (0, "QA / Test Automation Intern", "IT", "Tartu", true, ["C#", "Git", "English"]),
            (2, "Software Engineering Intern (Payments)", "IT", "Tallinn", false, ["Java", "SQL", "Git"]),
            (2, "Data Analyst Intern", "Finance", "Tallinn", false, ["Python", "SQL", "Data Analysis", "Excel"]),
            (2, "Accounting Assistant Intern", "Finance", "Tallinn", false, ["Accounting", "Excel", "English"]),
            (2, "Python Developer Intern", "IT", "Remote", true, ["Python", "Git", "Docker"]),
            (2, "Customer Support Intern (Russian)", "Customer Service", "Narva", false, ["Customer Service", "Russian", "English"]),
            (1, "Digital Marketing Intern", "Marketing", "Tartu", false, ["Marketing", "Social Media", "Copywriting"]),
            (1, "Social Media Content Intern", "Marketing", "Tartu", true, ["Social Media", "Copywriting", "Adobe Photoshop"]),
            (1, "Marketing Data Intern", "Marketing", "Tallinn", false, ["Marketing", "Excel", "Data Analysis"]),
            (1, "Copywriter Intern (English)", "Marketing", "Remote", true, ["Copywriting", "English"]),
            (3, "UI/UX Design Intern", "Design", "Pärnu", false, ["Figma", "UI/UX Design"]),
            (3, "Graphic Design Intern", "Design", "Pärnu", false, ["Adobe Photoshop", "Figma"]),
            (3, "Web Designer Intern", "Design", "Remote", true, ["HTML/CSS", "Figma", "UI/UX Design"]),
            (3, "Frontend + Design Intern", "IT", "Pärnu", false, ["HTML/CSS", "JavaScript", "Figma"]),
            (4, "Logistics Coordinator Intern", "Logistics", "Maardu", false, ["Logistics", "Excel", "English"]),
            (4, "Warehouse Operations Intern", "Logistics", "Maardu", false, ["Logistics", "Customer Service"]),
            (4, "Supply Chain Analyst Intern", "Logistics", "Tallinn", false, ["Logistics", "Data Analysis", "Excel"]),
            (4, "Engineering Drafting Intern", "Engineering", "Tallinn", false, ["AutoCAD", "English"]),
            (4, "IT Support Intern", "IT", "Maardu", false, ["Linux", "Customer Service", "Estonian"]),
            (0, "Project Management Intern", "Business", "Tallinn", false, ["Project Management", "English", "Excel"])
        ];

        return data.Select((d, index) =>
        {
            var start = DateTime.UtcNow.Date.AddMonths(2 + index % 3);
            var internship = new Internship
            {
                CompanyProfile = companies[d.C],
                CompanyProfileId = companies[d.C].Id,
                Title = d.Title,
                Field = d.Field,
                Location = d.Location,
                IsRemote = d.Remote,
                Description = $"{d.Title} at {companies[d.C].CompanyName}. You will work in a small team, " +
                              "get a mentor and take part in real projects.",
                Requirements = $"Required skills: {string.Join(", ", d.Skills)}.",
                StartDate = start,
                EndDate = start.AddMonths(2),
                ApplicationDeadline = DateTime.UtcNow.Date.AddDays(14 + index * 2),
                // A couple of drafts/closed listings so status filtering has something to show.
                Status = index switch
                {
                    22 => InternshipStatus.Draft,
                    23 => InternshipStatus.Closed,
                    _ => InternshipStatus.Published
                }
            };
            foreach (var name in d.Skills)
            {
                internship.InternshipSkills.Add(new InternshipSkill { Internship = internship, Skill = skills[name] });
            }
            return internship;
        }).ToList();
    }

    private List<Application> CreateApplications(List<StudentProfile> students, List<Internship> internships)
    {
        // (student index, internship index, status)
        (int S, int I, ApplicationStatus Status)[] data =
        [
            (0, 0, ApplicationStatus.Interview), (0, 1, ApplicationStatus.Submitted), (0, 4, ApplicationStatus.Viewed),
            (1, 2, ApplicationStatus.Accepted), (1, 17, ApplicationStatus.Submitted),
            (2, 6, ApplicationStatus.Viewed), (2, 8, ApplicationStatus.Rejected),
            (3, 5, ApplicationStatus.Submitted), (3, 3, ApplicationStatus.Interview), (3, 8, ApplicationStatus.Submitted),
            (4, 14, ApplicationStatus.Accepted), (4, 16, ApplicationStatus.Viewed),
            (5, 10, ApplicationStatus.Submitted), (5, 11, ApplicationStatus.Rejected),
            (6, 7, ApplicationStatus.Interview), (6, 9, ApplicationStatus.Submitted),
            (7, 18, ApplicationStatus.Viewed), (7, 19, ApplicationStatus.Withdrawn)
        ];

        return data.Select(d => new Application
        {
            StudentProfile = students[d.S],
            StudentProfileId = students[d.S].Id,
            Internship = internships[d.I],
            InternshipId = internships[d.I].Id,
            Status = d.Status,
            CoverLetter = $"Hello! I am {students[d.S].User.FirstName}, a {students[d.S].StudyProgram} student, " +
                          $"and I would love to join \"{internships[d.I].Title}\".",
            AppliedAt = DateTime.UtcNow.AddDays(-_random.Next(1, 20))
        }).ToList();
    }

    private static string Ascii(string s) => s
        .Replace("ä", "a").Replace("ö", "o").Replace("õ", "o").Replace("ü", "u")
        .Replace("Ä", "A").Replace("Ö", "O").Replace("Õ", "O").Replace("Ü", "U");
}
