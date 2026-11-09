using BuildingBlocks.Abstractions;

namespace Payroll.Application;

/// <summary>
/// Who may read a payslip: anyone with <c>payroll.read.all</c>, or the employee themself with <c>payroll.read.own</c>.
/// Checked before looking the employee up, so a caller without access learns nothing about whether the id exists.
/// </summary>
public sealed class PayslipAccessPolicy(ICurrentUser currentUser)
{
    public const string ReadAllPermission = "payroll.read.all";
    public const string ReadOwnPermission = "payroll.read.own";
    public const string EmployeeIdClaim = "employee_id";

    public Result EnsureCanRead(Guid employeeId)
    {
        if (currentUser.HasPermission(ReadAllPermission))
            return Result.Success();

        var isOwnPayslip = currentUser.HasPermission(ReadOwnPermission)
            && Guid.TryParse(currentUser.FindClaim(EmployeeIdClaim), out var ownEmployeeId)
            && ownEmployeeId == employeeId;

        return isOwnPayslip
            ? Result.Success()
            : Error.Forbidden("You can only view your own payslip.");
    }
}
