using BuildingBlocks.Abstractions;
using BuildingBlocks.Application;
using BuildingBlocks.Data;
using Employees.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EmployeePayroll.Api.Persistence;

/// <summary>Lets <c>dotnet ef migrations add</c> build the model without starting the host or a database.</summary>
internal sealed class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=design-time;Database=design-time",
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", AppDbContext.PlatformSchema))
            .Options;

        IModuleModelConfiguration[] modules = [new EmployeesModelConfiguration(), new IdentityModelConfiguration()];
        return new AppDbContext(options, new DesignTimeUser(), new SystemClock(), modules);
    }

    private sealed class DesignTimeUser : ICurrentUser
    {
        public bool IsAuthenticated => false;
        public Guid? Id => null;
        public string? UserName => null;
        public IReadOnlyCollection<string> Permissions => [];
        public bool HasPermission(string permission) => false;
        public string? FindClaim(string claimType) => null;
    }
}
