using BuildingBlocks.Abstractions;
using BuildingBlocks.Application;
using BuildingBlocks.Web;
using Employees.Application;
using Employees.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Employees.Infrastructure;

internal static class EmployeesEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var employees = api.MapGroup("/employees").WithTags("Employees");

        employees.MapGet("/", async (int? page, int? pageSize, string? search, string? department, string? status,
                ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListEmployeesQuery(page, pageSize, search, department, status), ct)).ToHttpResult())
            .WithName("ListEmployees")
            .RequirePermission(EmployeePermissions.Read)
            .Produces<PagedList<EmployeeListItem>>()
            .ProducesValidationProblem();

        employees.MapGet("/departments", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListDepartmentsQuery(), ct)).ToHttpResult())
            .WithName("ListDepartments")
            .RequirePermission(EmployeePermissions.Read)
            .Produces<IReadOnlyList<string>>();

        employees.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetEmployeeByIdQuery(id), ct)).ToHttpResult())
            .WithName("GetEmployee")
            .RequirePermission(EmployeePermissions.Read)
            .Produces<EmployeeDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        employees.MapPost("/", async (CreateEmployeeCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToCreatedHttpResult(created => $"/api/employees/{created.Id}"))
            .WithName("CreateEmployee")
            .RequirePermission(EmployeePermissions.Write)
            .Produces<EmployeeDetails>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        employees.MapPut("/{id:guid}", async (Guid id, UpdateEmployeeCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command with { Id = id }, ct)).ToHttpResult())
            .WithName("UpdateEmployee")
            .RequirePermission(EmployeePermissions.Write)
            .Produces<EmployeeDetails>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        employees.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DeleteEmployeeCommand(id), ct)).ToNoContentHttpResult())
            .WithName("DeleteEmployee")
            .RequirePermission(EmployeePermissions.Write)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
