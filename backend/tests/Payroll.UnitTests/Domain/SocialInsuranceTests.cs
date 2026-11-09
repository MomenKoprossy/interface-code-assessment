using AwesomeAssertions;
using Payroll.Domain;
using static Payroll.UnitTests.TestData;

namespace Payroll.UnitTests.Domain;

/// <summary>SI = round(base × rate), capped at the monthly cap (default 11%, cap 1100.00).</summary>
public class SocialInsuranceTests
{
    [Fact]
    public void Below_the_cap_is_not_capped() =>
        Payslip(Employee(5000m)).SocialInsurance.Should().Be(new SocialInsuranceLine(0.11m, 1100.00m, 550.00m, 550.00m, false));

    [Fact]
    public void Exactly_at_the_cap_is_not_capped() =>
        Payslip(Employee(10000m)).SocialInsurance.Should().Be(new SocialInsuranceLine(0.11m, 1100.00m, 1100.00m, 1100.00m, false));

    [Fact]
    public void One_cent_over_the_cap_is_capped() =>
        // 10000.10 × 11% = 1100.011 → 1100.01 > 1100.00
        Payslip(Employee(10000.10m)).SocialInsurance.Should().Be(new SocialInsuranceLine(0.11m, 1100.00m, 1100.01m, 1100.00m, true));

    [Fact]
    public void Above_the_cap_is_capped_and_shows_the_uncapped_amount() =>
        Payslip(Employee(12000m)).SocialInsurance.Should().Be(new SocialInsuranceLine(0.11m, 1100.00m, 1320.00m, 1100.00m, true));

    [Fact]
    public void Without_a_cap_it_is_never_capped()
    {
        var policy = DefaultPolicy with { SocialInsurance = new SocialInsurancePolicy(0.11m, null) };

        Payslip(Employee(100000m), policy).SocialInsurance.Should().Be(new SocialInsuranceLine(0.11m, null, 11000.00m, 11000.00m, false));
    }

    [Fact]
    public void Is_computed_on_base_salary_only_not_allowances() =>
        Payslip(Employee(5000m, allowances: [new("Housing", 1000m)])).SocialInsurance.Amount.Should().Be(550.00m);

    [Fact]
    public void Midpoint_rounds_away_from_zero()
    {
        // 100.05 × 10% = 10.005 → 10.01 (banker's rounding would give 10.00)
        var policy = DefaultPolicy with { SocialInsurance = new SocialInsurancePolicy(0.10m, null) };

        Payslip(Employee(100.05m), policy).SocialInsurance.Amount.Should().Be(10.01m);
    }

    [Fact]
    public void Rounds_sub_cent_results()
    {
        // 1234.56 × 11% = 135.8016 → 135.80
        Payslip(Employee(1234.56m)).SocialInsurance.Amount.Should().Be(135.80m);
    }
}
