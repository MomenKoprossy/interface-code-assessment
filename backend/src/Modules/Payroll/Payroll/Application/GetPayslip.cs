using BuildingBlocks.Abstractions;
using BuildingBlocks.Application;
using Employees.Contracts;
using FluentValidation;
using Payroll.Domain;

namespace Payroll.Application;

/// <summary>An employee's payslip for a month (<c>YYYY-MM</c>).</summary>
public sealed record GetPayslipQuery(Guid EmployeeId, string? Month) : IQuery<Result<Payslip>>;

internal sealed class GetPayslipValidator : AbstractValidator<GetPayslipQuery>
{
    public GetPayslipValidator() =>
        RuleFor(q => q.Month)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Month is required.")
            .Must(month => PayrollMonth.TryParse(month, out _)).WithMessage(PayrollMonth.FormatMessage);
}

internal sealed class GetPayslipHandler(
    PayslipAccessPolicy accessPolicy,
    IEmployeePayrollProfileProvider employees,
    IPayrollPolicyProvider policies,
    PayrollCalculator calculator) : IRequestHandler<GetPayslipQuery, Result<Payslip>>
{
    public async ValueTask<Result<Payslip>> Handle(GetPayslipQuery query, CancellationToken cancellationToken)
    {
        var access = accessPolicy.EnsureCanRead(query.EmployeeId);
        if (access.IsFailure)
            return access.Error!.Value;

        if (!PayrollMonth.TryParse(query.Month, out var month))
            return Error.Validation(PayrollMonth.FormatMessage, "month");

        var employee = await employees.GetAsync(query.EmployeeId, cancellationToken);
        if (employee is null)
            return Error.NotFound("Employee was not found.");

        return calculator.Calculate(policies.Current, employee, month);
    }
}
