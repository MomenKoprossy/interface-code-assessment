using System.Reflection;

namespace Payroll;

/// <summary>Assembly handle used by the host to register this module's handlers and validators.</summary>
public static class PayrollAssembly
{
    public static readonly Assembly Reference = typeof(PayrollAssembly).Assembly;
}
