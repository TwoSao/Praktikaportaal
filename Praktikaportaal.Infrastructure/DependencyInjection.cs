using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Praktikaportaal.Core.Interfaces;
using Praktikaportaal.Core.Interfaces.Repositories;
using Praktikaportaal.Core.Services;
using Praktikaportaal.Infrastructure.Options;
using Praktikaportaal.Infrastructure.Persistence;
using Praktikaportaal.Infrastructure.Persistence.Repositories;
using Praktikaportaal.Infrastructure.Persistence.Seed;
using Praktikaportaal.Infrastructure.Services;

namespace Praktikaportaal.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("DefaultConnection");

        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddDbContext<PraktikaportaalDbContext>(options =>
                options.UseMySql(connectionString, new MariaDbServerVersion(new Version(11, 4, 0)), sql =>
                    sql.MigrationsAssembly(typeof(PraktikaportaalDbContext).Assembly.FullName)));
        }

        // Strongly-typed JWT settings, validated when the host starts (fails fast on a missing/short secret).
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<FileStorageOptions>()
            .Bind(configuration.GetSection(FileStorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Repositories (data access) + unit of work
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IInternshipRepository, InternshipRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IStudentProfileRepository, StudentProfileRepository>();
        services.AddScoped<ISkillRepository, SkillRepository>();
        services.AddScoped<ICvRepository, CvRepository>();
        services.AddScoped<IStatisticsRepository, StatisticsRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();

        // Infrastructure services
        services.AddScoped<DbSeeder>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        // Business services (Core)
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IInternshipService, InternshipService>();
        services.AddScoped<IStudentService, StudentService>();
        services.AddScoped<ICvService, CvService>();
        services.AddScoped<IRecommendationService, RecommendationService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<ISkillService, SkillService>();

        return services;
    }
}
