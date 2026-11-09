namespace Payroll.Domain.Rules;

/// <summary>4. The employee's recurring post-tax deductions.</summary>
public sealed class OtherDeductionsRule : IPayrollRule
{
    public void Apply(PayslipDraft draft)
    {
        draft.OtherDeductions = draft.Employee.Deductions.Select(d => new PayItem(d.Name, d.Amount)).ToList();
        draft.TotalOtherDeductions = draft.OtherDeductions.Sum(d => d.Amount);
    }
}
