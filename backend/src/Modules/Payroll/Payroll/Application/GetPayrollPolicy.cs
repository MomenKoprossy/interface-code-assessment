using BuildingBlocks.Abstractions;
using BuildingBlocks.Application;
using Payroll.Domain;

namespace Payroll.Application;

/// <summary>Read-only view of the configured payroll rules. Shape = <c>PayrollPolicy</c> in docs/api-contract.md.</summary>
public sealed record PayrollPolicyView(
    string Currency,
    bool DeductSocialInsuranceBeforeTax,
    SocialInsurancePolicy SocialInsurance,
    IReadOnlyList<TaxBracket> TaxBrackets,
    string RoundingRule);

public sealed record GetPayrollPolicyQuery : IQuery<Result<PayrollPolicyView>>;

internal sealed class GetPayrollPolicyHandler(IPayrollPolicyProvider policies)
    : IRequestHandler<GetPayrollPolicyQuery, Result<PayrollPolicyView>>
{
    public ValueTask<Result<PayrollPolicyView>> Handle(GetPayrollPolicyQuery query, CancellationToken cancellationToken)
    {
        var policy = policies.Current;
        Result<PayrollPolicyView> view = new PayrollPolicyView(
            policy.Currency,
            policy.DeductSocialInsuranceBeforeTax,
            policy.SocialInsurance,
            policy.TaxBrackets,
            MoneyRounding.RuleDescription);
        return ValueTask.FromResult(view);
    }
}
