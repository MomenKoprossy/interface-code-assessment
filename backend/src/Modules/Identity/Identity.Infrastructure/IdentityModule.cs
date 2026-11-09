using BuildingBlocks.Application;
using BuildingBlocks.Data;
using BuildingBlocks.Web;
using Identity.Application;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Security;
using Identity.Infrastructure.Seeding;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure;

/// <summary>Composition of the Identity module: called once by the host.</summary>
public static class IdentityModule
{
    public static IHostApplicationBuilder AddIdentityModule(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddSingleton<IModuleModelConfiguration, IdentityModelConfiguration>();

        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IDataSeeder, IdentitySeeder>();

        return builder;
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Auth");

        auth.MapPost("/login", async (LoginCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToHttpResult())
            .WithName("Login")
            .AllowAnonymous()
            // Policy registered by the platform (SecurityRegistration): a tighter per-IP limit than the global one.
            .RequireRateLimiting(SecurityRegistration.LoginRateLimitPolicy)
            .Produces<LoginResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        auth.MapGet("/me", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetCurrentUserQuery(), ct)).ToHttpResult())
            .WithName("GetCurrentUser")
            .RequireAuthorization()
            .Produces<CurrentUserDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return api;
    }
}
