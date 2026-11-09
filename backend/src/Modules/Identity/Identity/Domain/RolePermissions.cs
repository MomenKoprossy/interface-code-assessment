using Employees.Contracts;
using Payroll.Contracts;

namespace Identity.Domain;

/// <summary>
/// The fixed role → permission map (ADR-008). Permissions travel in the JWT as repeated <c>permission</c> claims.
/// Permission names are owned by the modules that enforce them (their <c>*.Contracts</c>), so they can't drift.
/// </summary>
public static class RolePermissions
{
    private static readonly string[] Hr = [EmployeePermissions.Read, EmployeePermissions.Write, PayrollPermissions.ReadAll];
    private static readonly string[] Employee = [PayrollPermissions.ReadOwn];

    public static IReadOnlyList<string> For(Role role) => role switch
    {
        Role.HR => Hr,
        Role.Employee => Employee,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown role."),
    };
}
