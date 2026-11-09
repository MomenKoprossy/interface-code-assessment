using Employees.Domain;

namespace Employees.Application;

public sealed record PayItemDto(string Name, decimal Amount);

public sealed record EmployeeListItem(
    Guid Id,
    string FullName,
    string Email,
    string JobTitle,
    string Department,
    DateOnly HireDate,
    decimal BaseMonthlySalary,
    EmployeeStatus Status);

public sealed record EmployeeDetails(
    Guid Id,
    string FullName,
    string Email,
    string JobTitle,
    string Department,
    DateOnly HireDate,
    decimal BaseMonthlySalary,
    EmployeeStatus Status,
    IReadOnlyList<PayItemDto> Allowances,
    IReadOnlyList<PayItemDto> Deductions);

internal static class EmployeeMapping
{
    public static EmployeeListItem ToListItem(this Employee e) =>
        new(e.Id, e.FullName, e.Email, e.JobTitle, e.Department, e.HireDate, e.BaseMonthlySalary, e.Status);

    public static EmployeeDetails ToDetails(this Employee e) =>
        new(e.Id, e.FullName, e.Email, e.JobTitle, e.Department, e.HireDate, e.BaseMonthlySalary, e.Status,
            e.Allowances.Select(i => new PayItemDto(i.Name, i.Amount)).ToList(),
            e.Deductions.Select(i => new PayItemDto(i.Name, i.Amount)).ToList());
}
