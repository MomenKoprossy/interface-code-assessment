using BuildingBlocks.Data;
using Microsoft.EntityFrameworkCore;

namespace EmployeePayroll.Api.Persistence;

/// <summary>Applies pending migrations, then (when <c>Seed:Enabled</c>) runs every module's seeder in order.</summary>
internal static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        await services.GetRequiredService<AppDbContext>().Database.MigrateAsync(cancellationToken);

        if (!app.Configuration.GetValue<bool>("Seed:Enabled"))
            return;

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseInitializer));
        foreach (var seeder in services.GetServices<IDataSeeder>().OrderBy(s => s.Order))
        {
            logger.LogInformation("Running seeder {Seeder}", seeder.GetType().Name);
            await seeder.SeedAsync(cancellationToken);
        }
    }
}
