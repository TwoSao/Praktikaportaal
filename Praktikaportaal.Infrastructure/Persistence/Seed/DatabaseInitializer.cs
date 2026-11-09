using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Praktikaportaal.Infrastructure.Persistence.Seed;

public static class DatabaseInitializer
{
    /// <summary>
    /// Applies pending EF Core migrations and seeds demo data.
    /// Called on API startup when "Database:MigrateAndSeedOnStartup" is true.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PraktikaportaalDbContext>();

        await context.Database.MigrateAsync(ct);
        await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync(ct);
    }
}
