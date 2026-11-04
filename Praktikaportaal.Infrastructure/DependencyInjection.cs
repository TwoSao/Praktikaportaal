using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Praktikaportaal.Core.Interfaces;
using Praktikaportaal.Infrastructure.Persistence;
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
                options.UseSqlServer(connectionString, sql =>
                    sql.MigrationsAssembly(typeof(PraktikaportaalDbContext).Assembly.FullName)));
        }

        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IInternshipService, InternshipService>();
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<ISkillService, SkillService>();

        return services;
    }
}
