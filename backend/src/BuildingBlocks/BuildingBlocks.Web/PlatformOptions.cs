using System.Reflection;

namespace BuildingBlocks.Web;

/// <summary>
/// Opt-in configuration for <c>AddPlatform()</c>. A service only wires what it needs; the web edge
/// (ProblemDetails, exception handling, mediator, security, HTTP logging) is always on.
/// </summary>
public sealed class PlatformOptions
{
    /// <summary>Assemblies scanned for mediator handlers + FluentValidation validators.</summary>
    public List<Assembly> HandlerAssemblies { get; } = [];

    /// <summary>When true (default) a global fixed-window rate limiter is registered (429 on rejection).</summary>
    public bool UseRateLimiting { get; set; } = true;

    /// <summary>When true (default) NetEscapades security headers (CSP/HSTS/X-Content-Type-Options/referrer) are applied.</summary>
    public bool UseSecurityHeaders { get; set; } = true;
}
