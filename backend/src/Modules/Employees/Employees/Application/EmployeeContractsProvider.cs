using Employees.Contracts;
using Employees.Domain;

namespace Employees.Application;

public sealed class EmployeeContractsProvider(IEmployeeRepository employees)
    : IEmployeePayrollProfileProvider, IEmployeeDirectory
{
    public async Task<EmployeePayrollProfile?> GetAsync(Guid employeeId, CancellationToken cancellationToken = default) =>
        await employees.FindAsync(employeeId, cancellationToken) is { } employee ? ToProfile(employee) : null;

    public Task<Guid?> FindIdByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        employees.FindIdByEmailAsync(Employee.NormalizeEmail(email), cancellationToken);

    private static EmployeePayrollProfile ToProfile(Employee e) => new(
        e.Id,
        e.FullName,
        e.Department,
        e.JobTitle,
        e.HireDate,
        e.IsActive,
        e.BaseMonthlySalary,
        e.Allowances.Select(i => new PayComponent(i.Name, i.Amount)).ToList(),
        e.Deductions.Select(i => new PayComponent(i.Name, i.Amount)).ToList());
}
