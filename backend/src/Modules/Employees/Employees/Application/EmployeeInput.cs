using Employees.Domain;

namespace Employees.Application;

/// <summary>
/// The <c>EmployeeInput</c> request body shared by create and update. Everything is nullable so a missing field is
/// reported by the validator ("is required") instead of silently defaulting.
/// </summary>
public abstract record EmployeeInput
{
    public string? FullName { get; init; }
    public string? Email { get; init; }
    public string? JobTitle { get; init; }
    public string? Department { get; init; }
    public DateOnly? HireDate { get; init; }
    public decimal? BaseMonthlySalary { get; init; }
    public string? Status { get; init; }
    public IReadOnlyList<PayItemInput>? Allowances { get; init; }
    public IReadOnlyList<PayItemInput>? Deductions { get; init; }

    /// <summary>Only valid after <see cref="EmployeeInputValidator{T}"/> passed (the mediator guarantees it).</summary>
    internal EmployeeData ToData() => new(
        FullName!,
        Email!,
        JobTitle!,
        Department!,
        HireDate!.Value,
        BaseMonthlySalary!.Value,
        Enum.Parse<EmployeeStatus>(Status!),
        ToPayItems(Allowances),
        ToPayItems(Deductions));

    private static List<PayItem> ToPayItems(IReadOnlyList<PayItemInput>? items) =>
        items?.Select(i => new PayItem(i!.Name!, i.Amount!.Value)).ToList() ?? [];
}

public sealed record PayItemInput(string? Name, decimal? Amount);
