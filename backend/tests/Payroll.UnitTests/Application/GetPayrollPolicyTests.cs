using AwesomeAssertions;
using NSubstitute;
using Payroll.Application;
using Payroll.Domain;
using static Payroll.UnitTests.TestData;

namespace Payroll.UnitTests.Application;

public class GetPayrollPolicyTests
{
    [Fact]
    public async Task Returns_the_configured_policy_with_the_rounding_rule()
    {
        var policies = Substitute.For<IPayrollPolicyProvider>();
        policies.Current.Returns(DefaultPolicy);

        var result = await new GetPayrollPolicyHandler(policies).Handle(new GetPayrollPolicyQuery(), CancellationToken.None);

        result.Value.Should().BeEquivalentTo(new
        {
            Currency = "USD",
            DeductSocialInsuranceBeforeTax = true,
            SocialInsurance = new SocialInsurancePolicy(0.11m, 1100.00m),
            TaxBrackets = DefaultPolicy.TaxBrackets,
            RoundingRule = MoneyRounding.RuleDescription,
        });
    }
}
