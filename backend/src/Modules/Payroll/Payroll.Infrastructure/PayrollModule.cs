using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Payroll.Application;
using Payroll.Domain;
using Payroll.Domain.Rules;

namespace Payroll.Infrastructure;

/// <summary>Composition of the Payroll module: called once by the host. Payroll has no tables.</summary>
public static class PayrollModule
{
    public static IHostApplicationBuilder AddPayrollModule(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddOptions<PayrollOptions>()
            .Bind(builder.Configuration.GetSection(PayrollOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<PayrollOptions>, PayrollOptionsValidator>();
        services.AddSingleton<IPayrollPolicyProvider, ConfiguredPayrollPolicyProvider>();

        // Pipeline order = registration order. A new deduction type is one more line here.
        services.AddSingleton<IPayrollRule, GrossEarningsRule>();
        services.AddSingleton<IPayrollRule, SocialInsuranceRule>();
        services.AddSingleton<IPayrollRule, IncomeTaxRule>();
        services.AddSingleton<IPayrollRule, OtherDeductionsRule>();
        services.AddSingleton<PayrollCalculator>();

        services.AddScoped<PayslipAccessPolicy>();
        return builder;
    }

    public static IEndpointRouteBuilder MapPayrollEndpoints(this IEndpointRouteBuilder api) => PayrollEndpoints.Map(api);
}
