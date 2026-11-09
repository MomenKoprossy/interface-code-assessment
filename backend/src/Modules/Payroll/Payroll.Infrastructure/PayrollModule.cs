using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace Payroll.Infrastructure;

/// <summary>Composition of the Payroll module: called once by the host. Payroll has no tables.</summary>
public static class PayrollModule
{
    public static IHostApplicationBuilder AddPayrollModule(this IHostApplicationBuilder builder)
    {
        // SCAFFOLD: build:payroll binds + validates the Payroll options (ValidateOnStart) and registers the
        // policy provider, the calculator and the ordered IPayrollRule pipeline here.
        return builder;
    }

    public static IEndpointRouteBuilder MapPayrollEndpoints(this IEndpointRouteBuilder api)
    {
        // SCAFFOLD: build:payroll maps /employees/{id}/payslip and /payroll/policy here.
        return api;
    }
}
