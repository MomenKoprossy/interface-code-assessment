using AwesomeAssertions;
using Employees.Contracts;
using Payroll.Domain;
using Payroll.Domain.Rules;

namespace Payroll.UnitTests;

internal static class TestData
{
    public static readonly Guid EmployeeId = Guid.Parse("7d7c1c2e-5b1a-4f4e-9a65-0c1f1c2b3a4d");
    public static readonly DateOnly HireDate = new(2020, 1, 15);

    /// <summary>The default policy from docs/payroll-rules.md / appsettings.json.</summary>
    public static readonly PayrollPolicy DefaultPolicy = new(
        "USD",
        DeductSocialInsuranceBeforeTax: true,
        new SocialInsurancePolicy(0.11m, 1100.00m),
        [new TaxBracket(1500.00m, 0.00m), new TaxBracket(5000.00m, 0.10m), new TaxBracket(10000.00m, 0.20m), new TaxBracket(null, 0.25m)]);

    /// <summary>Default brackets with no social insurance, so taxable income = gross pay.</summary>
    public static readonly PayrollPolicy NoSocialInsurancePolicy = DefaultPolicy with { SocialInsurance = new SocialInsurancePolicy(0m, null) };

    public static EmployeePayrollProfile Employee(
        decimal baseSalary = 5000m,
        PayComponent[]? allowances = null,
        PayComponent[]? deductions = null,
        bool isActive = true,
        DateOnly? hireDate = null) =>
        new(EmployeeId, "Ada Lovelace", "Engineering", "Software Engineer", hireDate ?? HireDate, isActive,
            baseSalary, allowances ?? [], deductions ?? []);

    /// <summary>Culture-independent decimal from an [InlineData] string.</summary>
    public static decimal Dec(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    public static PayrollCalculator Calculator() =>
        new([new GrossEarningsRule(), new SocialInsuranceRule(), new IncomeTaxRule(), new OtherDeductionsRule()]);

    public static PayrollMonth Month(string value)
    {
        PayrollMonth.TryParse(value, out var month).Should().BeTrue();
        return month;
    }

    public static Payslip Payslip(EmployeePayrollProfile employee, PayrollPolicy? policy = null, string month = "2026-03")
    {
        var result = Calculator().Calculate(policy ?? DefaultPolicy, employee, Month(month));
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error!.Value.Message : string.Empty);
        return result.Value;
    }
}
