using System.Text;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Payroll.Application;
using Payroll.Domain;
using Payroll.Domain.Rules;
using Payroll.Infrastructure;

namespace Payroll.UnitTests.Infrastructure;

/// <summary>Configuration binding, startup validation and pipeline wiring of <see cref="PayrollModule"/>.</summary>
public class PayrollModuleTests
{
    // Same shape as the Payroll section of backend/src/Api/appsettings.json.
    private const string DefaultSection = """
        {
          "Payroll": {
            "Currency": "USD",
            "DeductSocialInsuranceBeforeTax": true,
            "SocialInsurance": { "Rate": 0.11, "MonthlyCap": 1100.00 },
            "TaxBrackets": [
              { "UpTo": 1500.00,  "Rate": 0.00 },
              { "UpTo": 5000.00,  "Rate": 0.10 },
              { "UpTo": 10000.00, "Rate": 0.20 },
              { "UpTo": null,     "Rate": 0.25 }
            ]
          }
        }
        """;

    [Fact]
    public void Binds_the_appsettings_shape_into_the_policy()
    {
        using var services = BuildServices(DefaultSection);

        var policy = services.GetRequiredService<IPayrollPolicyProvider>().Current;

        policy.Should().BeEquivalentTo(TestData.DefaultPolicy, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Registers_the_rules_in_pipeline_order()
    {
        using var services = BuildServices(DefaultSection);

        services.GetServices<IPayrollRule>().Select(r => r.GetType()).Should().Equal(
            typeof(GrossEarningsRule), typeof(SocialInsuranceRule), typeof(IncomeTaxRule), typeof(OtherDeductionsRule));
        services.GetRequiredService<PayrollCalculator>().Should().NotBeNull();
    }

    [Fact]
    public void Invalid_policy_fails_options_validation_with_every_problem()
    {
        const string invalid = """
            { "Payroll": { "Currency": "US", "SocialInsurance": { "Rate": 1.5, "MonthlyCap": -1 },
                           "TaxBrackets": [ { "UpTo": 5000, "Rate": 0.1 }, { "UpTo": 1500, "Rate": 0 } ] } }
            """;
        using var services = BuildServices(invalid);

        var act = () => services.GetRequiredService<IOptions<PayrollOptions>>().Value;

        act.Should().Throw<OptionsValidationException>()
            .Which.Failures.Should().HaveCount(5).And.AllSatisfy(f => f.Should().StartWith("Payroll: "));
    }

    [Fact]
    public void Missing_section_fails_validation_instead_of_paying_with_an_empty_policy()
    {
        using var services = BuildServices("{}");

        var act = () => services.GetRequiredService<IOptions<PayrollOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public async Task Validation_runs_at_startup()
    {
        var builder = Builder("{}");
        using var host = builder.Build();

        var act = () => host.StartAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<OptionsValidationException>();
    }

    private static ServiceProvider BuildServices(string json) => Builder(json).Services.BuildServiceProvider();

    private static HostApplicationBuilder Builder(string json)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)));
        builder.AddPayrollModule();
        return builder;
    }
}
