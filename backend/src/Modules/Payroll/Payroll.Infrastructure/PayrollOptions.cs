using Microsoft.Extensions.Options;
using Payroll.Application;
using Payroll.Domain;

namespace Payroll.Infrastructure;

/// <summary>
/// The <c>Payroll</c> configuration section (appsettings.json). Validated by the domain at startup. Required values are
/// nullable so a missing key is reported instead of silently binding to 0/false.
/// </summary>
public sealed class PayrollOptions
{
    public const string SectionName = "Payroll";

    public string? Currency { get; set; }
    public bool? DeductSocialInsuranceBeforeTax { get; set; }
    public SocialInsuranceOptions SocialInsurance { get; set; } = new();
    public List<TaxBracketOptions> TaxBrackets { get; set; } = [];

    public PayrollPolicy ToPolicy() => new(
        Currency ?? string.Empty,
        DeductSocialInsuranceBeforeTax ?? false,
        new SocialInsurancePolicy(SocialInsurance.Rate ?? 0, SocialInsurance.MonthlyCap),
        TaxBrackets.Select(b => new TaxBracket(b.UpTo, b.Rate ?? 0)).ToList());

    public sealed class SocialInsuranceOptions
    {
        public decimal? Rate { get; set; }
        public decimal? MonthlyCap { get; set; }
    }

    public sealed class TaxBracketOptions
    {
        public decimal? UpTo { get; set; }
        public decimal? Rate { get; set; }
    }
}

/// <summary>Reports missing keys, then delegates to <see cref="PayrollPolicy.Validate"/>, so a bad policy stops the app at startup.</summary>
internal sealed class PayrollOptionsValidator : IValidateOptions<PayrollOptions>
{
    public ValidateOptionsResult Validate(string? name, PayrollOptions options)
    {
        var problems = MissingKeys(options).Concat(options.ToPolicy().Validate()).ToList();
        return problems.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(problems.Select(p => $"{PayrollOptions.SectionName}: {p}"));
    }

    private static IEnumerable<string> MissingKeys(PayrollOptions options)
    {
        if (options.DeductSocialInsuranceBeforeTax is null)
            yield return "DeductSocialInsuranceBeforeTax is required.";
        if (options.SocialInsurance.Rate is null)
            yield return "SocialInsurance.Rate is required.";
        for (var i = 0; i < options.TaxBrackets.Count; i++)
            if (options.TaxBrackets[i].Rate is null)
                yield return $"TaxBrackets[{i}].Rate is required.";
    }
}

internal sealed class ConfiguredPayrollPolicyProvider(IOptions<PayrollOptions> options) : IPayrollPolicyProvider
{
    public PayrollPolicy Current { get; } = options.Value.ToPolicy();
}
