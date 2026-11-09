using BuildingBlocks.Abstractions;
using BuildingBlocks.Application;
using Employees.Domain;
using FluentValidation;

namespace Employees.Application;

public sealed record GetEmployeeByIdQuery(Guid Id) : IQuery<Result<EmployeeDetails>>;

internal sealed class GetEmployeeByIdHandler(IEmployeeRepository employees)
    : IRequestHandler<GetEmployeeByIdQuery, Result<EmployeeDetails>>
{
    public async ValueTask<Result<EmployeeDetails>> Handle(GetEmployeeByIdQuery query, CancellationToken cancellationToken) =>
        await employees.FindAsync(query.Id, cancellationToken) is { } employee
            ? employee.ToDetails()
            : EmployeeErrors.NotFound;
}

/// <summary>Raw query-string values; the handler normalizes paging and blank filters.</summary>
public sealed record ListEmployeesQuery(int? Page, int? PageSize, string? Search, string? Department, string? Status)
    : IQuery<Result<PagedList<EmployeeListItem>>>
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;
}

internal sealed class ListEmployeesValidator : AbstractValidator<ListEmployeesQuery>
{
    public ListEmployeesValidator() =>
        RuleFor(x => x.Status)
            .Must(status => Enum.GetNames<EmployeeStatus>().Contains(status))
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage("Status must be Active or Inactive.");
}

internal sealed class ListEmployeesHandler(IEmployeeRepository employees)
    : IRequestHandler<ListEmployeesQuery, Result<PagedList<EmployeeListItem>>>
{
    public async ValueTask<Result<PagedList<EmployeeListItem>>> Handle(ListEmployeesQuery query, CancellationToken cancellationToken)
    {
        var page = new PageRequest(
            Math.Max(query.Page ?? 1, 1),
            Math.Clamp(query.PageSize ?? ListEmployeesQuery.DefaultPageSize, 1, ListEmployeesQuery.MaxPageSize),
            ListEmployeesQuery.MaxPageSize);

        var criteria = new EmployeeSearchCriteria(
            NullIfBlank(query.Search),
            NullIfBlank(query.Department),
            string.IsNullOrWhiteSpace(query.Status) ? null : Enum.Parse<EmployeeStatus>(query.Status));

        var result = await employees.SearchAsync(criteria, page, cancellationToken);
        return new PagedList<EmployeeListItem>(
            result.Items.Select(e => e.ToListItem()).ToList(), result.Page, result.PageSize, result.TotalCount);
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record ListDepartmentsQuery : IQuery<Result<IReadOnlyList<string>>>;

internal sealed class ListDepartmentsHandler(IEmployeeRepository employees)
    : IRequestHandler<ListDepartmentsQuery, Result<IReadOnlyList<string>>>
{
    public async ValueTask<Result<IReadOnlyList<string>>> Handle(ListDepartmentsQuery query, CancellationToken cancellationToken) =>
        Result.Success(await employees.ListDepartmentsAsync(cancellationToken));
}
