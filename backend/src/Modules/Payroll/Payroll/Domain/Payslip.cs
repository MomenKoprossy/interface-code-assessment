namespace Payroll.Domain;

/// <summary>One employee's computed pay for one month. Shape = the <c>Payslip</c> type in docs/api-contract.md.</summary>
public sealed record Payslip(
    Guid EmployeeId,
    string EmployeeName,
    string Department,
    string JobTitle,
    string Month,
    string Currency,
    decimal BaseSalary,
    IReadOnlyList<PayItem> Allowances,
    decimal TotalAllowances,
    decimal GrossPay,
    SocialInsuranceLine SocialInsurance,
    decimal TaxableIncome,
    IncomeTaxLine IncomeTax,
    IReadOnlyList<PayItem> OtherDeductions,
    decimal TotalOtherDeductions,
    decimal TotalDeductions,
    decimal NetPay,
    string RoundingRule);

public sealed record PayItem(string Name, decimal Amount);

public sealed record SocialInsuranceLine(decimal Rate, decimal? MonthlyCap, decimal UncappedAmount, decimal Amount, bool CapApplied);

public sealed record IncomeTaxLine(decimal Amount, IReadOnlyList<TaxBracketLine> Brackets);

/// <summary>Tax of one bracket: <see cref="TaxedAmount"/> is the part of taxable income inside (From, UpTo].</summary>
public sealed record TaxBracketLine(decimal From, decimal? UpTo, decimal Rate, decimal TaxedAmount, decimal Tax);
