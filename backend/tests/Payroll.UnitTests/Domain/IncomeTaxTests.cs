using AwesomeAssertions;
using Employees.Contracts;
using Payroll.Domain;
using Payroll.Domain.Rules;
using static Payroll.UnitTests.TestData;

namespace Payroll.UnitTests.Domain;

/// <summary>
/// Progressive brackets (0-1500 @0%, 1500-5000 @10%, 5000-10000 @20%, above @25%) with inclusive upper bounds.
/// Uses a policy without social insurance, so taxable income = base salary.
/// </summary>
public class IncomeTaxTests
{
    [Theory]
    [InlineData("0.01", "0.00")]          // below the first bracket's top
    [InlineData("1000.00", "0.00")]
    [InlineData("1500.00", "0.00")]       // exactly on a boundary: all of it at the lower (0%) rate
    [InlineData("1500.01", "0.00")]       // first cent above: 0.01 × 10% = 0.001 → 0.00
    [InlineData("1500.05", "0.01")]       // 0.05 × 10% = 0.005 → 0.01 (midpoint, away from zero)
    [InlineData("3000.05", "150.01")]     // 1500.05 × 10% = 150.005 → 150.01
    [InlineData("5000.00", "350.00")]     // 3500 × 10%
    [InlineData("5000.01", "350.00")]     // + 0.01 × 20% = 0.002 → 0.00
    [InlineData("10000.00", "1350.00")]   // 350 + 5000 × 20%
    [InlineData("10000.04", "1350.01")]   // + 0.04 × 25% = 0.01
    [InlineData("12000.00", "1850.00")]   // 350 + 1000 + 2000 × 25%
    public void Taxes_each_portion_at_its_bracket_rate(string taxable, string expectedTax)
    {
        var payslip = Payslip(Employee(Dec(taxable)), NoSocialInsurancePolicy);

        payslip.TaxableIncome.Should().Be(Dec(taxable));
        payslip.IncomeTax.Amount.Should().Be(Dec(expectedTax));
        payslip.IncomeTax.Amount.Should().Be(payslip.IncomeTax.Brackets.Sum(b => b.Tax));
        payslip.IncomeTax.Brackets.Sum(b => b.TaxedAmount).Should().Be(Dec(taxable));
    }

    [Fact]
    public void Income_exactly_on_a_boundary_stays_entirely_in_the_lower_bracket()
    {
        var payslip = Payslip(Employee(5000.00m), NoSocialInsurancePolicy);

        payslip.IncomeTax.Brackets.Should().Equal(
            new TaxBracketLine(0m, 1500.00m, 0.00m, 1500.00m, 0.00m),
            new TaxBracketLine(1500.00m, 5000.00m, 0.10m, 3500.00m, 350.00m));
    }

    [Fact]
    public void The_first_cent_above_a_boundary_starts_the_next_bracket()
    {
        var payslip = Payslip(Employee(1500.01m), NoSocialInsurancePolicy);

        payslip.IncomeTax.Brackets.Should().Equal(
            new TaxBracketLine(0m, 1500.00m, 0.00m, 1500.00m, 0.00m),
            new TaxBracketLine(1500.00m, 5000.00m, 0.10m, 0.01m, 0.00m));
    }

    [Fact]
    public void Open_ended_top_bracket_taxes_everything_above_the_last_bound()
    {
        var payslip = Payslip(Employee(12000.00m), NoSocialInsurancePolicy);

        payslip.IncomeTax.Brackets[^1].Should().Be(new TaxBracketLine(10000.00m, null, 0.25m, 2000.00m, 500.00m));
    }

    [Fact]
    public void Zero_taxable_income_has_no_tax_and_no_bracket_lines()
    {
        var draft = new PayslipDraft(Employee(), DefaultPolicy)
        {
            GrossPay = 0m,
            SocialInsurance = new SocialInsuranceLine(0.11m, 1100m, 0m, 0m, false),
        };

        new IncomeTaxRule().Apply(draft);

        draft.TaxableIncome.Should().Be(0m);
        draft.IncomeTax!.Amount.Should().Be(0m);
        draft.IncomeTax.Brackets.Should().BeEmpty();
    }

    [Fact]
    public void Social_insurance_of_the_whole_salary_leaves_zero_taxable_and_zero_net()
    {
        var policy = DefaultPolicy with { SocialInsurance = new SocialInsurancePolicy(1m, null) };

        var payslip = Payslip(Employee(3000m), policy);

        payslip.TaxableIncome.Should().Be(0m);
        payslip.IncomeTax.Amount.Should().Be(0m);
        payslip.IncomeTax.Brackets.Should().BeEmpty();
        payslip.NetPay.Should().Be(0m);
    }

    [Fact]
    public void Social_insurance_is_deducted_before_tax_when_the_policy_says_so()
    {
        // gross 6000, SI 660 → taxable 5340 → 350 + 340 × 20% (68) = 418
        var payslip = Payslip(Employee(6000m), DefaultPolicy with { DeductSocialInsuranceBeforeTax = true });

        payslip.TaxableIncome.Should().Be(5340.00m);
        payslip.IncomeTax.Amount.Should().Be(418.00m);
        payslip.NetPay.Should().Be(6000m - 660m - 418m);
    }

    [Fact]
    public void Social_insurance_is_not_deducted_before_tax_otherwise()
    {
        // gross 6000 is taxable as a whole → 350 + 1000 × 20% (200) = 550
        var payslip = Payslip(Employee(6000m), DefaultPolicy with { DeductSocialInsuranceBeforeTax = false });

        payslip.TaxableIncome.Should().Be(6000.00m);
        payslip.IncomeTax.Amount.Should().Be(550.00m);
        payslip.NetPay.Should().Be(6000m - 660m - 550m);
    }

    [Fact]
    public void Allowances_are_taxable()
    {
        var payslip = Payslip(Employee(1500m, allowances: [new PayComponent("Housing", 500m)]), NoSocialInsurancePolicy);

        payslip.TaxableIncome.Should().Be(2000m);
        payslip.IncomeTax.Amount.Should().Be(50.00m);
    }
}
