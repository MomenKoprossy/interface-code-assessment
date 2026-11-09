namespace Payroll.Domain;

/// <summary>The configured payroll rules (docs/payroll-rules.md, "Configuration"). Pure data plus its own validation.</summary>
public sealed record PayrollPolicy(
    string Currency,
    bool DeductSocialInsuranceBeforeTax,
    SocialInsurancePolicy SocialInsurance,
    IReadOnlyList<TaxBracket> TaxBrackets)
{
    /// <summary>Every problem with this policy; empty when it is valid.</summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        if (Currency is not { Length: 3 } || !Currency.All(char.IsAsciiLetter))
            problems.Add("Currency must be a 3-letter code.");
        if (!IsRate(SocialInsurance.Rate))
            problems.Add("SocialInsurance.Rate must be between 0 and 1.");
        if (SocialInsurance.MonthlyCap is <= 0)
            problems.Add("SocialInsurance.MonthlyCap must be null (no cap) or greater than 0.");
        if (SocialInsurance.MonthlyCap is { } cap && !HasAtMostTwoDecimals(cap))
            problems.Add("SocialInsurance.MonthlyCap must have at most 2 decimals.");

        if (TaxBrackets.Count == 0)
        {
            problems.Add("At least one tax bracket is required.");
            return problems;
        }

        decimal previousUpTo = 0;
        for (var i = 0; i < TaxBrackets.Count; i++)
        {
            var bracket = TaxBrackets[i];
            var isLast = i == TaxBrackets.Count - 1;

            if (!IsRate(bracket.Rate))
                problems.Add($"TaxBrackets[{i}].Rate must be between 0 and 1.");

            if (bracket.UpTo is not { } upTo)
            {
                if (!isLast)
                    problems.Add($"TaxBrackets[{i}].UpTo may be null only on the last bracket.");
                continue;
            }

            if (isLast)
                problems.Add($"TaxBrackets[{i}].UpTo must be null: the last bracket is open-ended.");
            if (!HasAtMostTwoDecimals(upTo))
                problems.Add($"TaxBrackets[{i}].UpTo must have at most 2 decimals.");
            if (upTo <= previousUpTo)
                problems.Add($"TaxBrackets[{i}].UpTo must be greater than {previousUpTo} (strictly ascending, starting above 0).");
            previousUpTo = upTo;
        }

        return problems;
    }

    private static bool IsRate(decimal rate) => rate is >= 0 and <= 1;

    // Money amounts flow onto the payslip as-is, so they must already be whole cents.
    private static bool HasAtMostTwoDecimals(decimal amount) => decimal.Round(amount, 2) == amount;
}

/// <summary>Social insurance: <see cref="Rate"/> of the base salary, capped at <see cref="MonthlyCap"/> when set.</summary>
public sealed record SocialInsurancePolicy(decimal Rate, decimal? MonthlyCap);

/// <summary>A progressive tax bracket covering (previous UpTo, UpTo]; null UpTo means no upper bound.</summary>
public sealed record TaxBracket(decimal? UpTo, decimal Rate);
