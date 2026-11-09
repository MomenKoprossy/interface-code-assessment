using BuildingBlocks.Abstractions;
using BuildingBlocks.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BuildingBlocks.Web;

/// <summary>
/// The web-edge aggregation (§5): <c>AddPlatform()</c> wires ProblemDetails, the global exception
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

        services.AddProblemDetails();
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
    /// NOTE on transport (§13.3): this is an internal microservices topology where TLS terminates at the
    /// gateway and service-to-service traffic flows over plaintext http resolved by service discovery
    /// (http://cms, http://attachment). Services therefore do NOT self-redirect to HTTPS — a
    /// UseHttpsRedirection here would 307 every internal call and break routing. Edge TLS + HSTS are the
    /// gateway/host's concern; the Strict-Transport-Security header is still emitted by UseSecurityHeaders.
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
