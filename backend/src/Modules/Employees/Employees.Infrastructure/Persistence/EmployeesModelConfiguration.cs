using BuildingBlocks.Data;
using Microsoft.EntityFrameworkCore;

namespace Employees.Infrastructure.Persistence;

/// <summary>The Employees module's slice of the shared EF model: owns the <c>employees</c> schema.</summary>
public sealed class EmployeesModelConfiguration : IModuleModelConfiguration
{
    public const string Schema = "employees";

    public void Configure(ModelBuilder modelBuilder)
    {
        // SCAFFOLD: build:employees applies the entity configurations of this assembly here.
    }
}
