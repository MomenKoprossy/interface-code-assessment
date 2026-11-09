namespace Employees.Contracts;

/// <summary>Permissions guarding the Employees endpoints; granted to roles by the Identity module.</summary>
public static class EmployeePermissions
{
    public const string Read = "employees.read";
    public const string Write = "employees.write";
}

/// <summary>Claim linking a login to its employee record: issued by Identity, read by Payroll's own-payslip rule.</summary>
public static class EmployeeClaimTypes
{
    public const string EmployeeId = "employee_id";
}
