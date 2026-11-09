using BuildingBlocks.Application;
using BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Payroll.Application;
using Payroll.Domain;

namespace Payroll.Infrastructure;

/// <summary>
/// Payroll HTTP endpoints. Both only require an authenticated caller; the payslip handler applies the
/// fine-grained rule (read all, or read own) so it can answer 403 before revealing whether an employee exists.
/// </summary>
internal static class PayrollEndpoints
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup(string.Empty).WithTags("Payroll").RequireAuthorization();

        group.MapGet("/employees/{id:guid}/payslip",
                async (Guid id, [FromQuery] string? month, ISender sender, CancellationToken cancellationToken) =>
                    (await sender.Send(new GetPayslipQuery(id, month), cancellationToken)).ToHttpResult())
            .WithName("GetPayslip")
            .WithSummary("Computes an employee's payslip for a month (YYYY-MM).")
            .WithDescription("HR (payroll.read.all) may read any payslip; an Employee (payroll.read.own) only their own.")
            .Produces<Payslip>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/payroll/policy",
                async (ISender sender, CancellationToken cancellationToken) =>
                    (await sender.Send(new GetPayrollPolicyQuery(), cancellationToken)).ToHttpResult())
            .WithName("GetPayrollPolicy")
            .WithSummary("The configured payroll rules (read-only).")
            .Produces<PayrollPolicyView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return api;
    }
}
