using BuildingBlocks.Data;
using Employees.Infrastructure.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Employees.Infrastructure;

/// <summary>Composition of the Employees module: called once by the host.</summary>
public static class EmployeesModule
{
    public static IHostApplicationBuilder AddEmployeesModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IModuleModelConfiguration, EmployeesModelConfiguration>();
        // SCAFFOLD: build:employees registers repositories, the Employees.Contracts implementations,
        // the unique-email exception mapper and the seeder here.
        return builder;
    }

    public static IEndpointRouteBuilder MapEmployeesEndpoints(this IEndpointRouteBuilder api)
    {
        // SCAFFOLD: build:employees maps /employees endpoints here (api is already grouped under /api).
        return api;
    }
}
