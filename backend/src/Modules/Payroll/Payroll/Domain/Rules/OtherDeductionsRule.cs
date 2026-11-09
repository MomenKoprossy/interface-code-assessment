namespace Payroll.Domain.Rules;

/// <summary>
/// 4. The employee's recurring post-tax deductions. Appends, so another post-tax deduction rule may run before or
/// after it.
/// </summary>
public sealed class OtherDeductionsRule : IPayrollRule
{
    public void Apply(PayslipDraft draft)
    {
        var items = draft.Employee.Deductions.Select(d => new PayItem(d.Name, d.Amount)).ToList();
        draft.OtherDeductions = [.. draft.OtherDeductions, .. items];
        draft.TotalOtherDeductions += items.Sum(d => d.Amount);
    }
}
