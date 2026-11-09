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
/// Security baseline (§13): JWT bearer (symmetric key from config section <c>Jwt</c>), the permission
/// policy provider + handler (super-admin short-circuit), a global fixed-window rate limiter, and
/// sensible security headers. Wired by <c>AddPlatform()</c>.
/// </summary>
public static class SecurityRegistration
{
    /// <summary>Default global rate-limit window: 100 requests / 10s per partition.</summary>
    private const int PermitLimit = 100;
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(10);

    public static IServiceCollection AddPlatformSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        AddJwtAuthentication(services, configuration);
        AddPermissionAuthorization(services);
        AddRateLimiting(services);
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

    private static void AddRateLimiting(IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = PermitLimit,
                        Window = Window,
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    }));
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
