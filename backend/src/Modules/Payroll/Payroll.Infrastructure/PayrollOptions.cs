using Microsoft.Extensions.Options;
using Payroll.Application;
using Payroll.Domain;

namespace Payroll.Infrastructure;

/// <summary>The <c>Payroll</c> configuration section (appsettings.json). Validated by the domain at startup.</summary>
public sealed class PayrollOptions
{
    public const string SectionName = "Payroll";

    public string? Currency { get; set; }
    public bool DeductSocialInsuranceBeforeTax { get; set; }
    public SocialInsuranceOptions SocialInsurance { get; set; } = new();
    public List<TaxBracketOptions> TaxBrackets { get; set; } = [];

    public PayrollPolicy ToPolicy() => new(
        Currency ?? string.Empty,
        DeductSocialInsuranceBeforeTax,
        new SocialInsurancePolicy(SocialInsurance.Rate, SocialInsurance.MonthlyCap),
        TaxBrackets.Select(b => new TaxBracket(b.UpTo, b.Rate)).ToList());

    public sealed class SocialInsuranceOptions
    {
        public decimal Rate { get; set; }
        public decimal? MonthlyCap { get; set; }
    }

    public sealed class TaxBracketOptions
    {
        public decimal? UpTo { get; set; }
        public decimal Rate { get; set; }
    }
}

/// <summary>Delegates to <see cref="PayrollPolicy.Validate"/>, so a bad policy stops the app at startup.</summary>
internal sealed class PayrollOptionsValidator : IValidateOptions<PayrollOptions>
{
    public ValidateOptionsResult Validate(string? name, PayrollOptions options)
    {
        var problems = options.ToPolicy().Validate();
        return problems.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(problems.Select(p => $"{PayrollOptions.SectionName}: {p}"));
    }
}

internal sealed class ConfiguredPayrollPolicyProvider(IOptions<PayrollOptions> options) : IPayrollPolicyProvider
{
    public PayrollPolicy Current { get; } = options.Value.ToPolicy();
}
