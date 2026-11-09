namespace Payroll.Domain.Rules;

/// <summary>
/// 3. Progressive income tax. Bracket i taxes the portion of taxable income in (UpTo[i-1], UpTo[i]] (upper bound
/// inclusive); each bracket's tax is rounded and the total is the sum of those lines. Only brackets the income
/// reaches are listed, so a 0% bracket shows when income passes through it and taxable income 0 lists none.
/// </summary>
public sealed class IncomeTaxRule : IPayrollRule
{
    public void Apply(PayslipDraft draft)
    {
        var socialInsurance = draft.SocialInsurance
            ?? throw new InvalidOperationException($"{nameof(SocialInsuranceRule)} must run before {nameof(IncomeTaxRule)}.");

        var taxable = draft.Policy.DeductSocialInsuranceBeforeTax
            ? draft.GrossPay - socialInsurance.Amount
            : draft.GrossPay;

        var lines = new List<TaxBracketLine>();
        decimal from = 0;
        foreach (var bracket in draft.Policy.TaxBrackets)
        {
            if (taxable <= from)
                break;

            var taxedAmount = Math.Min(taxable, bracket.UpTo ?? taxable) - from;
            lines.Add(new TaxBracketLine(from, bracket.UpTo, bracket.Rate, taxedAmount, MoneyRounding.Round(taxedAmount * bracket.Rate)));

            if (bracket.UpTo is not { } upTo)
                break;
            from = upTo;
        }

        draft.TaxableIncome = taxable;
        draft.IncomeTax = new IncomeTaxLine(lines.Sum(l => l.Tax), lines);
    }
}
