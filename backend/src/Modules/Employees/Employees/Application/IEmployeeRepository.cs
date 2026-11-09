using BuildingBlocks.Abstractions;
using Employees.Domain;

namespace Employees.Application;

/// <summary>Persistence port for employees. Deleted employees are invisible to every method.</summary>
public interface IEmployeeRepository
{
    Task<Employee?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The id of the employee using <paramref name="normalizedEmail"/>, or null.</summary>
    Task<Guid?> FindIdByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    /// <summary>A page of employees matching <paramref name="criteria"/>, ordered by full name, then id.</summary>
    Task<PagedList<Employee>> SearchAsync(EmployeeSearchCriteria criteria, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Distinct department names, sorted.</summary>
    Task<IReadOnlyList<string>> ListDepartmentsAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Employee employee, CancellationToken cancellationToken = default);

    void Remove(Employee employee);
}

/// <summary>
/// List filters. <paramref name="Search"/>: case-insensitive substring of full name or email.
/// <paramref name="Department"/>: exact, case-insensitive. Null means "no filter".
/// </summary>
public sealed record EmployeeSearchCriteria(string? Search, string? Department, EmployeeStatus? Status);
