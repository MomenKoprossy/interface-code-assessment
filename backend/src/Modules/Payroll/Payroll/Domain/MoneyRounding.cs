namespace Payroll.Domain;

/// <summary>The single place payroll amounts are rounded (see docs/payroll-rules.md, "Rounding rule").</summary>
public static class MoneyRounding
{
    public const string RuleDescription =
        "Every amount is rounded to 2 decimals, half away from zero (0.005 rounds to 0.01), when it is computed. " +
        "Totals are sums of the rounded lines.";

    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
