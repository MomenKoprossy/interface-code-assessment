using BuildingBlocks.Abstractions;

namespace Employees.Application;

public static class EmployeeErrors
{
    public const string EmailInUseMessage = "Email is already used by another employee.";

    public static readonly Error NotFound = Error.NotFound("Employee was not found.");
    public static readonly Error EmailInUse = Error.Conflict(EmailInUseMessage, field: "email");
}
