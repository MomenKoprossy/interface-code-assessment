using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Data;

/// <summary>
/// A module's contribution to the shared EF model (its schema, entity configurations). Registered in DI by the
/// module's Infrastructure and applied by the host's DbContext, so the host never references module entities.
/// </summary>
public interface IModuleModelConfiguration
{
    void Configure(ModelBuilder modelBuilder);
}

/// <summary>
/// Seeds data after migrations are applied. Run in ascending <see cref="Order"/> by the host at startup when
/// seeding is enabled. Implementations must be idempotent (safe on every start).
/// </summary>
public interface IDataSeeder
{
    int Order { get; }
    Task SeedAsync(CancellationToken cancellationToken);
}
