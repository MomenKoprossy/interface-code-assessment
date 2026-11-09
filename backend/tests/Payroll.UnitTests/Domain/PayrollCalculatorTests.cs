using AwesomeAssertions;
using BuildingBlocks.Abstractions;
using Employees.Contracts;
using Payroll.Domain;
using Payroll.Domain.Rules;
using static Payroll.UnitTests.TestData;

namespace Payroll.UnitTests.Domain;

/// <summary>The whole pipeline plus every row of the edge-case table in docs/payroll-rules.md.</summary>
public class PayrollCalculatorTests
{
    [Fact]
    public void Worked_example_matches_the_hand_computed_payslip()
    {
        // Default policy. Base 6000.00, allowances Housing 500.00 + Transport 250.50, deduction Loan 200.00.
        //   gross            = 6000.00 + 750.50                         = 6750.50
        //   social insurance = round(6000.00 × 0.11) = 660.00 (cap 1100.00 not reached)
        //   taxable          = 6750.50 − 660.00                         = 6090.50
        //   tax  0.00-1500.00   @ 0%  : 1500.00 →   0.00
        //        1500.00-5000.00 @ 10%: 3500.00 → 350.00
        //        5000.00-10000.00@ 20%: 1090.50 → 218.10
        //                                                        total = 568.10
        //   total deductions = 660.00 + 568.10 + 200.00                 = 1428.10
        //   net              = 6750.50 − 1428.10                        = 5322.40
        var employee = Employee(
            6000.00m,
            allowances: [new PayComponent("Housing", 500.00m), new PayComponent("Transport", 250.50m)],
            deductions: [new PayComponent("Loan", 200.00m)]);

        var payslip = Payslip(employee, month: "2026-03");

        payslip.Should().BeEquivalentTo(new
        {
            EmployeeId,
            EmployeeName = "Ada Lovelace",
            Department = "Engineering",
            JobTitle = "Software Engineer",
            Month = "2026-03",
            Currency = "USD",
            BaseSalary = 6000.00m,
            Allowances = new[] { new PayItem("Housing", 500.00m), new PayItem("Transport", 250.50m) },
            TotalAllowances = 750.50m,
            GrossPay = 6750.50m,
            SocialInsurance = new SocialInsuranceLine(0.11m, 1100.00m, 660.00m, 660.00m, false),
            TaxableIncome = 6090.50m,
            IncomeTax = new
            {
                Amount = 568.10m,
                Brackets = new[]
                {
                    new TaxBracketLine(0m, 1500.00m, 0.00m, 1500.00m, 0.00m),
                    new TaxBracketLine(1500.00m, 5000.00m, 0.10m, 3500.00m, 350.00m),
                    new TaxBracketLine(5000.00m, 10000.00m, 0.20m, 1090.50m, 218.10m),
                },
            },
            OtherDeductions = new[] { new PayItem("Loan", 200.00m) },
            TotalOtherDeductions = 200.00m,
            TotalDeductions = 1428.10m,
            NetPay = 5322.40m,
            RoundingRule = MoneyRounding.RuleDescription,
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Capped_example_matches_the_hand_computed_payslip()
    {
        // Base 12000.00, nothing else. SI round(12000 × 0.11) = 1320.00 → capped to 1100.00.
        // taxable 10900.00 → 0 + 350.00 + 1000.00 + 900.00 × 25% (225.00) = 1575.00
        // deductions 1100.00 + 1575.00 = 2675.00 → net 12000.00 − 2675.00 = 9325.00
        var payslip = Payslip(Employee(12000.00m));

        payslip.SocialInsurance.Should().Be(new SocialInsuranceLine(0.11m, 1100.00m, 1320.00m, 1100.00m, true));
        payslip.TaxableIncome.Should().Be(10900.00m);
        payslip.IncomeTax.Amount.Should().Be(1575.00m);
        payslip.TotalDeductions.Should().Be(2675.00m);
        payslip.NetPay.Should().Be(9325.00m);
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("1234.56")]
    [InlineData("1500.05")]
    [InlineData("3333.33")]
    [InlineData("9999.99")]
    [InlineData("10000.10")]
    [InlineData("77777.77")]
    [InlineData("1000000.00")]
    public void Totals_add_up_to_the_cent(string baseSalary)
    {
        var employee = Employee(
            Dec(baseSalary),
            allowances: [new PayComponent("A", 0.01m), new PayComponent("B", 123.45m)],
            deductions: [new PayComponent("C", 0.01m)]);

        var payslip = Payslip(employee);

        payslip.TotalAllowances.Should().Be(payslip.Allowances.Sum(a => a.Amount));
        payslip.GrossPay.Should().Be(payslip.BaseSalary + payslip.TotalAllowances);
        payslip.IncomeTax.Amount.Should().Be(payslip.IncomeTax.Brackets.Sum(b => b.Tax));
        payslip.TotalOtherDeductions.Should().Be(payslip.OtherDeductions.Sum(d => d.Amount));
        payslip.TotalDeductions.Should().Be(payslip.SocialInsurance.Amount + payslip.IncomeTax.Amount + payslip.TotalOtherDeductions);
        payslip.NetPay.Should().Be(payslip.GrossPay - payslip.TotalDeductions);
        foreach (var amount in new[] { payslip.SocialInsurance.Amount, payslip.IncomeTax.Amount, payslip.NetPay })
            amount.Should().Be(MoneyRounding.Round(amount), "every line has at most 2 decimals");
    }

    [Fact]
    public void Inactive_employee_is_unprocessable() =>
        ShouldFail(Employee(isActive: false), "2026-03", "Payslip is not available for an inactive employee.");

    [Fact]
    public void Month_before_the_hire_month_is_unprocessable() =>
        ShouldFail(Employee(hireDate: new DateOnly(2026, 3, 15)), "2026-02", "Employee was not yet hired in 2026-02.");

    [Theory]
    [InlineData(1)]   // hired on the first day
    [InlineData(15)]  // hired mid-month
    [InlineData(31)]  // hired on the last day
    public void Hire_month_pays_the_full_month_without_proration(int hireDay)
    {
        var payslip = Payslip(Employee(4000m, hireDate: new DateOnly(2026, 3, hireDay)), month: "2026-03");

        payslip.GrossPay.Should().Be(4000m);
        payslip.SocialInsurance.Amount.Should().Be(440.00m);
    }

    [Fact]
    public void Future_month_is_allowed_as_a_preview() =>
        Payslip(Employee(), month: "2099-12").Month.Should().Be("2099-12");

    [Fact]
    public void No_allowances_and_no_deductions_give_empty_lists_and_zero_totals()
    {
        var payslip = Payslip(Employee(5000m));

        payslip.Allowances.Should().BeEmpty();
        payslip.TotalAllowances.Should().Be(0m);
        payslip.OtherDeductions.Should().BeEmpty();
        payslip.TotalOtherDeductions.Should().Be(0m);
        payslip.GrossPay.Should().Be(5000m);
        // SI 550.00, taxable 4450.00 → (4450 − 1500) × 10% = 295.00, net 5000 − 845 = 4155.00
        payslip.NetPay.Should().Be(4155.00m);
    }

    [Fact]
    public void Deductions_exceeding_gross_are_unprocessable()
    {
        // base 1000: SI 110.00, taxable 890.00 → tax 0; 110 + 0 + 890.01 = 1000.01 > 1000
        ShouldFail(Employee(1000m, deductions: [new PayComponent("Loan", 890.01m)]), "2026-03", "Deductions exceed gross pay for 2026-03.");
    }

    [Fact]
    public void Deductions_equal_to_gross_give_a_zero_net_payslip() =>
        Payslip(Employee(1000m, deductions: [new PayComponent("Loan", 890.00m)])).NetPay.Should().Be(0m);

    [Fact]
    public void A_missing_rule_is_a_configuration_bug_not_a_payslip()
    {
        var calculator = new PayrollCalculator([new GrossEarningsRule(), new OtherDeductionsRule()]);

        var act = () => calculator.Calculate(DefaultPolicy, Employee(), Month("2026-03"));

        act.Should().Throw<InvalidOperationException>().WithMessage("*SocialInsuranceRule*");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_new_post_tax_deduction_is_one_rule_plus_one_registration(bool beforeOtherDeductions)
    {
        IPayrollRule[] rules = beforeOtherDeductions
            ? [new GrossEarningsRule(), new SocialInsuranceRule(), new IncomeTaxRule(), new PensionRule(), new OtherDeductionsRule()]
            : [new GrossEarningsRule(), new SocialInsuranceRule(), new IncomeTaxRule(), new OtherDeductionsRule(), new PensionRule()];
        var employee = Employee(6000.00m, deductions: [new PayComponent("Loan", 200.00m)]);
        var without = Payslip(employee);

        var payslip = new PayrollCalculator(rules).Calculate(DefaultPolicy, employee, Month("2026-03")).Value;

        payslip.OtherDeductions.Should().BeEquivalentTo([new PayItem("Loan", 200.00m), new PayItem("Pension", 50.00m)]);
        payslip.TotalOtherDeductions.Should().Be(250.00m);
        payslip.TotalDeductions.Should().Be(without.TotalDeductions + 50.00m);
        payslip.NetPay.Should().Be(without.NetPay - 50.00m);
    }

    /// <summary>A test-only post-tax deduction: proves the pipeline is open for new deduction types.</summary>
    private sealed class PensionRule : IPayrollRule
    {
        public void Apply(PayslipDraft draft)
        {
            draft.OtherDeductions = [.. draft.OtherDeductions, new PayItem("Pension", 50.00m)];
            draft.TotalOtherDeductions += 50.00m;
        }
    }

    private static void ShouldFail(EmployeePayrollProfile employee, string month, string message)
    {
        var result = Calculator().Calculate(DefaultPolicy, employee, Month(month));

        result.IsFailure.Should().BeTrue();
        result.Error!.Value.Type.Should().Be(ErrorType.Unprocessable);
        result.Error.Value.Message.Should().Be(message);
    }
}
