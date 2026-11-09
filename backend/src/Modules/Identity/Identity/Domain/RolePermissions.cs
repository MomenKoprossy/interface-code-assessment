namespace Identity.Domain;

public static class Permissions
{
    public const string EmployeesRead = "employees.read";
    public const string EmployeesWrite = "employees.write";
    public const string PayrollReadAll = "payroll.read.all";
    public const string PayrollReadOwn = "payroll.read.own";
}

/// <summary>The fixed role → permission map (ADR-008). Permissions travel in the JWT as repeated <c>permission</c> claims.</summary>
public static class RolePermissions
{
    private static readonly string[] Hr = [Permissions.EmployeesRead, Permissions.EmployeesWrite, Permissions.PayrollReadAll];
    private static readonly string[] Employee = [Permissions.PayrollReadOwn];

    public static IReadOnlyList<string> For(Role role) => role switch
    {
        Role.HR => Hr,
        Role.Employee => Employee,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown role."),
    };
}
