using BuildingBlocks.Data;
using Employees.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Employees.Infrastructure.Persistence;

/// <summary>The Employees module's slice of the shared EF model: owns the <c>employees</c> schema.</summary>
public sealed class EmployeesModelConfiguration : IModuleModelConfiguration
{
    public const string Schema = "employees";
    public const string UniqueEmailIndex = "ux_employees_email";

    public void Configure(ModelBuilder modelBuilder) => Configure(modelBuilder.Entity<Employee>());

    private static void Configure(EntityTypeBuilder<Employee> employee)
    {
        employee.ToTable("employees", Schema);
        employee.HasKey(e => e.Id);
        employee.Property(e => e.Id).ValueGeneratedNever();

        employee.Property(e => e.FullName).HasMaxLength(EmployeeRules.MaxTextLength).IsRequired();
        employee.Property(e => e.Email).HasMaxLength(EmployeeRules.MaxEmailLength).IsRequired();
        employee.Property(e => e.JobTitle).HasMaxLength(EmployeeRules.MaxTextLength).IsRequired();
        employee.Property(e => e.Department).HasMaxLength(EmployeeRules.MaxTextLength).IsRequired();
        employee.Property(e => e.BaseMonthlySalary).HasPrecision(18, 2);
        employee.Property(e => e.Status).HasConversion<string>().HasMaxLength(16);

        // JSON complex collections, not owned entities: the pay items are part of the employee row, so the soft
        // delete (Deleted flipped back to Modified) never cascades to them and a deleted employee keeps them.
        // Mapped through the backing fields because EF needs an IList<T>; the aggregate exposes IReadOnlyList<T>.
        employee.Ignore(e => e.Allowances);
        employee.Ignore(e => e.Deductions);
        employee.ComplexCollection<List<PayItem>, PayItem>("_allowances", items => items.ToJson("Allowances"));
        employee.ComplexCollection<List<PayItem>, PayItem>("_deductions", items => items.ToJson("Deductions"));

        // Emails are stored lower-cased, so this is case-insensitive; deleted employees free their email.
        employee.HasIndex(e => e.Email)
            .HasDatabaseName(UniqueEmailIndex)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        employee.HasIndex(e => e.FullName);
    }
}
