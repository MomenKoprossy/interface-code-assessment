using System.Reflection;

namespace BuildingBlocks.Web;

/// <summary>
/// Configuration for <c>AddPlatform()</c>. The web edge (ProblemDetails, exception handling, mediator, security,
/// HTTP logging) is always on; the host only says where its handlers live.
/// </summary>
public sealed class PlatformOptions
{
    /// <summary>Assemblies scanned for mediator handlers + FluentValidation validators.</summary>
    public List<Assembly> HandlerAssemblies { get; } = [];
}
