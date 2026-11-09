using Payroll.Domain;

namespace Payroll.Application;

/// <summary>Port: the payroll policy currently in force (implemented from validated configuration).</summary>
public interface IPayrollPolicyProvider
{
    PayrollPolicy Current { get; }
}
