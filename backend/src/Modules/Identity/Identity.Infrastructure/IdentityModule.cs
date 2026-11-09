using BuildingBlocks.Data;
using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Identity.Infrastructure;

/// <summary>Composition of the Identity module: called once by the host.</summary>
public static class IdentityModule
{
    public static IHostApplicationBuilder AddIdentityModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IModuleModelConfiguration, IdentityModelConfiguration>();
        // SCAFFOLD: build:identity registers JWT options (validated on start), the token issuer,
        // password hashing, the user repository and the seeder here.
        return builder;
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder api)
    {
        // SCAFFOLD: build:identity maps /auth endpoints here (api is already grouped under /api).
        return api;
    }
}
