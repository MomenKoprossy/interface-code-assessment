namespace Payroll.Domain.Rules;

/// <summary>2. social insurance = round(base × rate), capped at the monthly cap when one is configured.</summary>
public sealed class SocialInsuranceRule : IPayrollRule
{
    public void Apply(PayslipDraft draft)
    {
        var policy = draft.Policy.SocialInsurance;
        var uncapped = MoneyRounding.Round(draft.Employee.BaseMonthlySalary * policy.Rate);
        var capApplied = uncapped > policy.MonthlyCap;

        draft.SocialInsurance = new SocialInsuranceLine(
            policy.Rate,
            policy.MonthlyCap,
            uncapped,
            capApplied ? policy.MonthlyCap!.Value : uncapped,
            capApplied);
    }
}
