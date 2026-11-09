using System.Text;
using System.Threading.RateLimiting;
using BuildingBlocks.Abstractions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Web;

/// <summary>
/// Security baseline: JWT bearer (symmetric key from config section <c>Jwt</c>), the permission
/// policy provider + handler (exact permission claim), a global fixed-window rate limiter plus the named
/// <see cref="LoginRateLimitPolicy"/>, and sensible security headers. Wired by <c>AddPlatform()</c>.
/// </summary>
public static class SecurityRegistration
{
    /// <summary>
    /// Named rate-limit policy for the login endpoint (<c>.RequireRateLimiting("login")</c>): a fixed window of
    /// <c>RateLimiting:LoginPermitLimit</c> (default 10) attempts per minute per client IP, on top of the global limit.
    /// </summary>
    public const string LoginRateLimitPolicy = "login";

    /// <summary>Default global rate-limit window: 100 requests / 10s per client IP (overridable via the <c>RateLimiting</c> section).</summary>
    private const int DefaultPermitLimit = 100;
    private const int DefaultWindowSeconds = 10;
    private const int DefaultLoginPermitLimit = 10;

    public static IServiceCollection AddPlatformSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        AddJwtAuthentication(services, configuration);
        AddPermissionAuthorization(services);
        AddRateLimiting(services, configuration);
        AddSecurityHeaders(services);
        return services;
    }

    private static void AddJwtAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection("Jwt");
        var key = jwt["Key"] ?? string.Empty;

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false; // keep raw claim types (e.g. "sub", "permission")
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = jwt["Audience"],
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                    RoleClaimType = "role",
                    NameClaimType = "name",
                };
            });
    }

    private static void AddPermissionAuthorization(IServiceCollection services)
    {
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
    }

    private static void AddRateLimiting(IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue("RateLimiting:PermitLimit", DefaultPermitLimit);
        var window = TimeSpan.FromSeconds(configuration.GetValue("RateLimiting:WindowSeconds", DefaultWindowSeconds));
        var loginPermitLimit = configuration.GetValue("RateLimiting:LoginPermitLimit", DefaultLoginPermitLimit);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                PerClientIp(httpContext, permitLimit, window));
            options.AddPolicy(LoginRateLimitPolicy, httpContext =>
                PerClientIp(httpContext, loginPermitLimit, TimeSpan.FromMinutes(1)));
        });
    }

    private static RateLimitPartition<string> PerClientIp(HttpContext httpContext, int permitLimit, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            });

    private static void AddSecurityHeaders(IServiceCollection services) =>
        services.AddSecurityHeaderPolicies()
            .SetDefaultPolicy(policy =>
            {
                policy.AddDefaultSecurityHeaders(); // X-Content-Type-Options, referrer policy, frame options, HSTS, etc.
                policy.AddContentSecurityPolicy(csp =>
                {
                    csp.AddDefaultSrc().Self();
                    csp.AddFrameAncestors().None();
                });
            });
}
