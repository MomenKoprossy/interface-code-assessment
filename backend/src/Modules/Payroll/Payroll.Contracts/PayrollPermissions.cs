namespace Payroll.Contracts;

/// <summary>Permissions guarding payslips; granted to roles by the Identity module.</summary>
public static class PayrollPermissions
{
    /// <summary>Read any employee's payslip.</summary>
    public const string ReadAll = "payroll.read.all";

    /// <summary>Read only the payslip of the employee linked to the caller's login.</summary>
    public const string ReadOwn = "payroll.read.own";
}
