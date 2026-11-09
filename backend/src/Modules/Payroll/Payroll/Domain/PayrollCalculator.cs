using BuildingBlocks.Abstractions;
using Employees.Contracts;
using Payroll.Domain.Rules;

namespace Payroll.Domain;

/// <summary>
/// Pure function of (policy, employee profile, month): checks the employee can be paid for the month, runs the
/// ordered rule pipeline and totals the rounded lines.
/// </summary>
public sealed class PayrollCalculator(IEnumerable<IPayrollRule> rules)
{
    private readonly IReadOnlyList<IPayrollRule> _rules = rules.ToList();

    public Result<Payslip> Calculate(PayrollPolicy policy, EmployeePayrollProfile employee, PayrollMonth month)
    {
        if (!employee.IsActive)
            return Error.Unprocessable("Payslip is not available for an inactive employee.");
        // Assumption: hired mid-month gets the full month, no proration (documented limitation).
        if (month.LastDay < employee.HireDate)
            return Error.Unprocessable($"Employee was not yet hired in {month}.");

        var draft = new PayslipDraft(employee, policy);
        foreach (var rule in _rules)
            rule.Apply(draft);

        var socialInsurance = draft.SocialInsurance ?? throw MissingRule(nameof(SocialInsuranceRule));
        var incomeTax = draft.IncomeTax ?? throw MissingRule(nameof(IncomeTaxRule));

        var totalDeductions = socialInsurance.Amount + incomeTax.Amount + draft.TotalOtherDeductions;
        var netPay = draft.GrossPay - totalDeductions;
        if (netPay < 0)
            return Error.Unprocessable($"Deductions exceed gross pay for {month}.");

        return new Payslip(
            employee.EmployeeId,
            employee.FullName,
            employee.Department,
            employee.JobTitle,
            month.ToString(),
            policy.Currency,
            employee.BaseMonthlySalary,
            draft.Allowances,
            draft.TotalAllowances,
            draft.GrossPay,
            socialInsurance,
            draft.TaxableIncome,
            incomeTax,
            draft.OtherDeductions,
            draft.TotalOtherDeductions,
            totalDeductions,
            netPay,
            MoneyRounding.RuleDescription);
    }

    private static InvalidOperationException MissingRule(string rule) => new($"{rule} is not registered in the payroll pipeline.");
}
