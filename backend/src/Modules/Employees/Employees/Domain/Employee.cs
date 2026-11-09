using BuildingBlocks.Abstractions;
using BuildingBlocks.Domain;
using Employees.Contracts;

namespace Employees.Domain;

/// <summary>
/// An employee and the fixed monthly pay items Payroll reads. Input rules are enforced by the application
/// validator before the aggregate is built; the aggregate owns normalization and the deactivation event.
/// </summary>
public sealed class Employee : AggregateRoot<Guid>, IAuditable, ISoftDeletable
{
    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string JobTitle { get; private set; } = string.Empty;
    public string Department { get; private set; } = string.Empty;
    public DateOnly HireDate { get; private set; }
    public decimal BaseMonthlySalary { get; private set; }
    public EmployeeStatus Status { get; private set; }
    public IReadOnlyList<PayItem> Allowances => _allowances;
    public IReadOnlyList<PayItem> Deductions => _deductions;

    public DateTime? CreatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public Guid? ModifiedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }

    public bool IsActive => Status == EmployeeStatus.Active;

    private List<PayItem> _allowances = [];
    private List<PayItem> _deductions = [];

    private Employee() { }

    public static Employee Create(EmployeeData data)
    {
        var employee = new Employee { Id = Guid.CreateVersion7() };
        employee.Apply(data);
        return employee;
    }

    public void Update(EmployeeData data)
    {
        var wasActive = IsActive;
        Apply(data);

        if (wasActive && !IsActive)
            Raise(new EmployeeDeactivated(Id));
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private void Apply(EmployeeData data)
    {
        FullName = data.FullName.Trim();
        Email = NormalizeEmail(data.Email);
        JobTitle = data.JobTitle.Trim();
        Department = data.Department.Trim();
        HireDate = data.HireDate;
        BaseMonthlySalary = data.BaseMonthlySalary;
        Status = data.Status;
        _allowances = Normalize(data.Allowances);
        _deductions = Normalize(data.Deductions);
    }

    private static List<PayItem> Normalize(IEnumerable<PayItem> items) =>
        items.Select(item => item with { Name = item.Name.Trim() }).ToList();
}

/// <summary>Everything that defines an employee, as accepted by <see cref="Employee.Create"/> and <see cref="Employee.Update"/>.</summary>
public sealed record EmployeeData(
    string FullName,
    string Email,
    string JobTitle,
    string Department,
    DateOnly HireDate,
    decimal BaseMonthlySalary,
    EmployeeStatus Status,
    IReadOnlyList<PayItem> Allowances,
    IReadOnlyList<PayItem> Deductions);
