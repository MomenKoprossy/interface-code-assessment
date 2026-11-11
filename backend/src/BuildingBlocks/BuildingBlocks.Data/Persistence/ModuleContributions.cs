using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Data;

/// <summary>Registered in DI by a module and applied by the host's DbContext, so the host never references module entities.</summary>
public interface IModuleModelConfiguration
{
    void Configure(ModelBuilder modelBuilder);
}

/// <summary>
/// Run after migrations, in ascending <see cref="Order"/>, at startup when seeding is enabled.
/// Implementations must be idempotent (safe on every start).
/// </summary>
public interface IDataSeeder
{
    int Order { get; }
    Task SeedAsync(CancellationToken cancellationToken);
}
