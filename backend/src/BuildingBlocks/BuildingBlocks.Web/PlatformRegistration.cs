using BuildingBlocks.Abstractions;
using BuildingBlocks.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BuildingBlocks.Web;

/// <summary>
/// The web-edge aggregation: <c>AddPlatform()</c> wires ProblemDetails, the global exception
/// handler, the mediator pipeline, ambient identity/clock, HTTP logging, and the security baseline;
/// <c>UsePlatform()</c> applies the middleware in the one correct order.
/// </summary>
public static class PlatformRegistration
{
    /// <summary>
    /// Wires the always-on web edge. Generic over <see cref="IHostApplicationBuilder"/> so it works for
    /// web services and the Aspire host alike.
    /// </summary>
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
    /// Applies the platform middleware in order: exception handling → status-code pages → HTTP logging →
    /// security headers (which emit HSTS) → rate limiting → authentication → authorization.
    /// Status-code pages turn every bodiless error (JwtBearer 401 challenge, authorization 403, binding 400/415,
    /// rate-limit 429, unmatched 404) into RFC 7807 ProblemDetails, so clients get one error format everywhere.
    /// NOTE on transport: TLS terminates at the edge (nginx in docker compose, or a cloud load balancer), and the
    /// API serves plain HTTP behind it, so it does not call UseHttpsRedirection. The Strict-Transport-Security
    /// header is still emitted by UseSecurityHeaders.
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
