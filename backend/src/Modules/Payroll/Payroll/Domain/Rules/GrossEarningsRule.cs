namespace Payroll.Domain.Rules;

/// <summary>1. gross = base salary + allowances. Stored amounts already have at most 2 decimals, so no rounding.</summary>
public sealed class GrossEarningsRule : IPayrollRule
{
    public void Apply(PayslipDraft draft)
    {
        draft.Allowances = draft.Employee.Allowances.Select(a => new PayItem(a.Name, a.Amount)).ToList();
        draft.TotalAllowances = draft.Allowances.Sum(a => a.Amount);
        draft.GrossPay = draft.Employee.BaseMonthlySalary + draft.TotalAllowances;
    }
}
