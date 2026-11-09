namespace Employees.Contracts;

/// <summary>
/// What another module (Payroll) may know about an employee to compute pay. Read-only snapshot;
/// the Employees module owns the data and implements the provider.
/// </summary>
public sealed record EmployeePayrollProfile(
    Guid EmployeeId,
    string FullName,
    string Department,
    string JobTitle,
    DateOnly HireDate,
    bool IsActive,
    decimal BaseMonthlySalary,
    IReadOnlyList<PayComponent> Allowances,
    IReadOnlyList<PayComponent> Deductions);

/// <summary>A named fixed monthly amount (an allowance or an "other" deduction).</summary>
public sealed record PayComponent(string Name, decimal Amount);

/// <summary>Payroll's view of employees. Returns null when the employee doesn't exist (or was deleted).</summary>
public interface IEmployeePayrollProfileProvider
{
    Task<EmployeePayrollProfile?> GetAsync(Guid employeeId, CancellationToken cancellationToken = default);
}

/// <summary>Lookup used by other modules (e.g. Identity seeding links a login to an employee by email).</summary>
public interface IEmployeeDirectory
{
    Task<Guid?> FindIdByEmailAsync(string email, CancellationToken cancellationToken = default);
}
