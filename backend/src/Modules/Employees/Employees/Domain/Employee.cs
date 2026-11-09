using BuildingBlocks.Abstractions;
using BuildingBlocks.Domain;
using Employees.Contracts;

namespace Employees.Domain;

/// <summary>
/// An employee and the fixed monthly pay items Payroll reads. The aggregate protects its own invariants: every
/// <see cref="Create"/>/<see cref="Update"/> is checked against <see cref="EmployeeRules"/>. Those checks are
/// guards, not user validation: API input is validated first (with field messages) by the application validator
/// using the same rules, so reaching a guard means a caller skipped validation, which is a bug, and it throws.
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

    /// <param name="data">The employee's fields.</param>
    /// <param name="today">Today's date in UTC, for the "hire date not in the future" invariant.</param>
    public static Employee Create(EmployeeData data, DateOnly today)
    {
        var employee = new Employee { Id = Guid.CreateVersion7() };
        employee.Apply(data, today);
        return employee;
    }

    /// <inheritdoc cref="Create"/>
    public void Update(EmployeeData data, DateOnly today)
    {
        var wasActive = IsActive;
        Apply(data, today);

        if (wasActive && !IsActive)
            Raise(new EmployeeDeactivated(Id));
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private void Apply(EmployeeData data, DateOnly today)
    {
        EnsureValid(data, today);

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

    private static void EnsureValid(EmployeeData data, DateOnly today)
    {
        Guard(EmployeeRules.IsValidText(data.FullName, EmployeeRules.MaxTextLength), nameof(data.FullName));
        Guard(EmployeeRules.IsValidEmail(data.Email), nameof(data.Email));
        Guard(EmployeeRules.IsValidText(data.JobTitle, EmployeeRules.MaxTextLength), nameof(data.JobTitle));
        Guard(EmployeeRules.IsValidText(data.Department, EmployeeRules.MaxTextLength), nameof(data.Department));
        Guard(EmployeeRules.IsValidHireDate(data.HireDate, today), nameof(data.HireDate));
        Guard(EmployeeRules.IsValidMoney(data.BaseMonthlySalary), nameof(data.BaseMonthlySalary));
        Guard(Enum.IsDefined(data.Status), nameof(data.Status));
        Guard(data.Allowances is not null && EmployeeRules.IsValidPayItemList(data.Allowances), nameof(data.Allowances));
        Guard(data.Deductions is not null && EmployeeRules.IsValidPayItemList(data.Deductions), nameof(data.Deductions));
    }

    private static void Guard(bool holds, string field)
    {
        if (!holds)
            throw new ArgumentException($"Employee invariant violated for {field}; input must be validated before it reaches the aggregate.", field);
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
