using BuildingBlocks.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>The Identity module's slice of the shared EF model: owns the <c>identity</c> schema.</summary>
public sealed class IdentityModelConfiguration : IModuleModelConfiguration
{
    public const string Schema = "identity";

    public void Configure(ModelBuilder modelBuilder)
    {
        // SCAFFOLD: build:identity applies the entity configurations of this assembly here.
    }
}
