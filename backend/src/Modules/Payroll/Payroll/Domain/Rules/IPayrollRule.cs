using Employees.Contracts;

namespace Payroll.Domain.Rules;

/// <summary>
/// One step of the payslip pipeline. A rule reads what earlier rules wrote on the draft and writes its own lines.
/// Rules run in registration order (docs/payroll-rules.md, "Extensibility").
/// </summary>
public interface IPayrollRule
{
    void Apply(PayslipDraft draft);
}

/// <summary>The payslip under construction, passed through the ordered <see cref="IPayrollRule"/>s.</summary>
public sealed class PayslipDraft(EmployeePayrollProfile employee, PayrollPolicy policy)
{
    public EmployeePayrollProfile Employee { get; } = employee;
    public PayrollPolicy Policy { get; } = policy;

    public IReadOnlyList<PayItem> Allowances { get; set; } = [];
    public decimal TotalAllowances { get; set; }
    public decimal GrossPay { get; set; }
    public SocialInsuranceLine? SocialInsurance { get; set; }
    public decimal TaxableIncome { get; set; }
    public IncomeTaxLine? IncomeTax { get; set; }
    public IReadOnlyList<PayItem> OtherDeductions { get; set; } = [];
    public decimal TotalOtherDeductions { get; set; }
}
