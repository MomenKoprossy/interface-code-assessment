using AwesomeAssertions;
using Payroll.Domain;
using static Payroll.UnitTests.TestData;

namespace Payroll.UnitTests.Domain;

public class PayrollPolicyValidationTests
{
    private static readonly PayrollPolicy Valid = TestData.DefaultPolicy;

    [Fact]
    public void Default_policy_is_valid() => Valid.Validate().Should().BeEmpty();

    [Fact]
    public void Single_open_ended_bracket_and_no_cap_is_valid() =>
        (Valid with { SocialInsurance = new SocialInsurancePolicy(0m, null), TaxBrackets = [new TaxBracket(null, 0m)] })
            .Validate().Should().BeEmpty();

    [Fact]
    public void Rates_of_exactly_0_and_1_are_valid() =>
        (Valid with { SocialInsurance = new SocialInsurancePolicy(1m, null), TaxBrackets = [new TaxBracket(100m, 0m), new TaxBracket(null, 1m)] })
            .Validate().Should().BeEmpty();

    [Fact]
    public void Empty_brackets_fail() =>
        (Valid with { TaxBrackets = [] }).Validate().Should().ContainSingle().Which.Should().Contain("At least one tax bracket");

    [Fact]
    public void Unsorted_brackets_fail() =>
        (Valid with { TaxBrackets = [new TaxBracket(5000m, 0.1m), new TaxBracket(1500m, 0m), new TaxBracket(null, 0.2m)] })
            .Validate().Should().ContainSingle().Which.Should().Contain("TaxBrackets[1].UpTo must be greater than 5000");

    [Fact]
    public void Equal_upper_bounds_fail() =>
        (Valid with { TaxBrackets = [new TaxBracket(1500m, 0m), new TaxBracket(1500m, 0.1m), new TaxBracket(null, 0.2m)] })
            .Validate().Should().ContainSingle().Which.Should().Contain("strictly ascending");

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void First_upper_bound_must_be_above_zero(string upTo) =>
        (Valid with { TaxBrackets = [new TaxBracket(Dec(upTo), 0m), new TaxBracket(null, 0.1m)] })
            .Validate().Should().ContainSingle().Which.Should().Contain("TaxBrackets[0].UpTo");

    [Fact]
    public void Null_upper_bound_not_last_fails() =>
        (Valid with { TaxBrackets = [new TaxBracket(null, 0m), new TaxBracket(5000m, 0.1m), new TaxBracket(null, 0.2m)] })
            .Validate().Should().ContainSingle().Which.Should().Contain("TaxBrackets[0].UpTo may be null only on the last bracket");

    [Fact]
    public void Last_bracket_must_be_open_ended() =>
        (Valid with { TaxBrackets = [new TaxBracket(1500m, 0m), new TaxBracket(5000m, 0.1m)] })
            .Validate().Should().ContainSingle().Which.Should().Contain("TaxBrackets[1].UpTo must be null");

    [Theory]
    [InlineData("1.01")]
    [InlineData("-0.01")]
    public void Bracket_rate_outside_0_to_1_fails(string rate) =>
        (Valid with { TaxBrackets = [new TaxBracket(1500m, 0m), new TaxBracket(null, Dec(rate))] })
            .Validate().Should().ContainSingle().Which.Should().Contain("TaxBrackets[1].Rate");

    [Theory]
    [InlineData("1.01")]
    [InlineData("-0.01")]
    public void Social_insurance_rate_outside_0_to_1_fails(string rate) =>
        (Valid with { SocialInsurance = new SocialInsurancePolicy(Dec(rate), null) })
            .Validate().Should().ContainSingle().Which.Should().Contain("SocialInsurance.Rate");

    [Theory]
    [InlineData("0")]
    [InlineData("-100")]
    public void Cap_must_be_null_or_positive(string cap) =>
        (Valid with { SocialInsurance = new SocialInsurancePolicy(0.11m, Dec(cap)) })
            .Validate().Should().ContainSingle().Which.Should().Contain("MonthlyCap");

    [Fact]
    public void Cap_with_more_than_2_decimals_fails() =>
        (Valid with { SocialInsurance = new SocialInsurancePolicy(0.11m, 1100.005m) })
            .Validate().Should().ContainSingle().Which.Should().Be("SocialInsurance.MonthlyCap must have at most 2 decimals.");

    [Fact]
    public void Upper_bound_with_more_than_2_decimals_fails() =>
        (Valid with { TaxBrackets = [new TaxBracket(1500.005m, 0m), new TaxBracket(null, 0.1m)] })
            .Validate().Should().ContainSingle().Which.Should().Be("TaxBrackets[0].UpTo must have at most 2 decimals.");

    [Theory]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDT")]
    [InlineData("U5D")]
    public void Currency_must_be_three_letters(string currency) =>
        (Valid with { Currency = currency }).Validate().Should().ContainSingle().Which.Should().Contain("Currency");

    [Fact]
    public void Reports_every_problem_at_once() =>
        new PayrollPolicy("X", true, new SocialInsurancePolicy(2m, -1m), [new TaxBracket(null, 5m), new TaxBracket(10m, 0m)])
            .Validate().Should().HaveCount(6);
}
