using BuildingBlocks.Abstractions;
using Employees.Contracts;
using Payroll.Contracts;

namespace Payroll.Application;

/// <summary>
/// Who may read a payslip: anyone with <c>payroll.read.all</c>, or the employee themself with <c>payroll.read.own</c>.
/// Checked before looking the employee up, so a caller without access learns nothing about whether the id exists.
/// </summary>
public sealed class PayslipAccessPolicy(ICurrentUser currentUser)
{
    public Result EnsureCanRead(Guid employeeId)
    {
        if (currentUser.HasPermission(PayrollPermissions.ReadAll))
            return Result.Success();

        var isOwnPayslip = currentUser.HasPermission(PayrollPermissions.ReadOwn)
            && Guid.TryParse(currentUser.FindClaim(EmployeeClaimTypes.EmployeeId), out var ownEmployeeId)
            && ownEmployeeId == employeeId;

        return isOwnPayslip
            ? Result.Success()
            : Error.Forbidden("You can only view your own payslip.");
    }
}
