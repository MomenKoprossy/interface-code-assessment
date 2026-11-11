using BuildingBlocks.Data;
using BuildingBlocks.Web;
using Employees.Application;
using Employees.Contracts;
using Employees.Infrastructure.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Employees.Infrastructure;

public static class EmployeesModule
{
    public static IHostApplicationBuilder AddEmployeesModule(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddSingleton<IModuleModelConfiguration, EmployeesModelConfiguration>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<EmployeeContractsProvider>();
        services.AddScoped<IEmployeePayrollProfileProvider>(sp => sp.GetRequiredService<EmployeeContractsProvider>());
        services.AddScoped<IEmployeeDirectory>(sp => sp.GetRequiredService<EmployeeContractsProvider>());
        services.AddSingleton<IExceptionProblemMapper, UniqueEmailViolationMapper>();
        services.AddScoped<IDataSeeder, EmployeesSeeder>();
        return builder;
    }

    public static IEndpointRouteBuilder MapEmployeesEndpoints(this IEndpointRouteBuilder api)
    {
        EmployeesEndpoints.Map(api);
        return api;
    }
}
