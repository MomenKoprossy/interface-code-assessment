using BuildingBlocks.Abstractions;
using BuildingBlocks.Application;
using BuildingBlocks.Data;
using Microsoft.EntityFrameworkCore;

namespace EmployeePayroll.Api.Persistence;

/// <summary>
/// The single EF Core context of the modular monolith (ADR-004). It knows no module entity: each module
/// contributes its schema and mappings through <see cref="IModuleModelConfiguration"/>. Audit stamping,
/// soft delete and domain-event dispatch come from <see cref="AppDbContextBase"/>.
/// </summary>
public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentUser currentUser,
    IClock clock,
    IEnumerable<IModuleModelConfiguration> moduleConfigurations,
    IDomainEventDispatcher? dispatcher = null)
    : AppDbContextBase(options, currentUser, clock, dispatcher)
{
    public const string ConnectionStringName = "appdb";
    public const string PlatformSchema = "platform";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var module in moduleConfigurations)
            module.Configure(modelBuilder);

        base.OnModelCreating(modelBuilder);   // after modules: soft-delete filters + domain-event ignores see every entity
    }
}
