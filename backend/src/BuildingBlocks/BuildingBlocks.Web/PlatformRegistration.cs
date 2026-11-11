using BuildingBlocks.Abstractions;
using BuildingBlocks.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BuildingBlocks.Web;

public static class PlatformRegistration
{
    /// <summary>Generic over <see cref="IHostApplicationBuilder"/> so it works for web services and the Aspire host alike.</summary>
    public static TBuilder AddPlatform<TBuilder>(this TBuilder builder, Action<PlatformOptions>? configure = null)
        where TBuilder : IHostApplicationBuilder
    {
        var options = new PlatformOptions();
        configure?.Invoke(options);

        var services = builder.Services;

        // Every ProblemDetails gets a "type": ASP.NET only fills it for well-known codes, not e.g. 429.
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Type ??= context.ProblemDetails.Status == StatusCodes.Status429TooManyRequests
                ? "https://tools.ietf.org/html/rfc6585#section-4"
                : "about:blank");
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddPlatformMediator(options.HandlerAssemblies.ToArray());
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddPlatformHttpLogging();
        services.AddPlatformSecurity(builder.Configuration);

        return builder;
    }

    /// <summary>
    /// Order matters. Status-code pages turn every bodiless error (JwtBearer 401 challenge, authorization 403,
    /// binding 400/415, rate-limit 429, unmatched 404) into ProblemDetails, so clients get one error format.
    /// No UseHttpsRedirection: the compose demo serves plain HTTP, and in a real deployment TLS terminates at the
    /// edge (load balancer/ingress), which should send HSTS; this API adds HSTS only to requests it sees as HTTPS.
    /// </summary>
    public static WebApplication UsePlatform(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseHttpLogging();
        app.UseSecurityHeaders();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }
}
