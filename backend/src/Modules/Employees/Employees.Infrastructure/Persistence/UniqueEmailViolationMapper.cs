using BuildingBlocks.Web;
using Employees.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Employees.Infrastructure.Persistence;

/// <summary>
/// Race-condition backstop: two concurrent writes with the same email both pass the handler's check, and the
/// unique index rejects the second. That violation becomes the same 409 (with <c>errors.email</c>) the handler returns.
/// </summary>
internal sealed class UniqueEmailViolationMapper : IExceptionProblemMapper
{
    public ProblemDetails? TryMap(Exception exception) =>
        exception is DbUpdateException
        {
            InnerException: PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: EmployeesModelConfiguration.UniqueEmailIndex,
            },
        }
            ? new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Detail = EmployeeErrors.EmailInUseMessage,
                Extensions = { ["errors"] = new Dictionary<string, string[]> { ["email"] = [EmployeeErrors.EmailInUseMessage] } },
            }
            : null;
}
