using System.Reflection;

namespace BuildingBlocks.Web;

public sealed class PlatformOptions
{
    /// <summary>Assemblies scanned for mediator handlers + FluentValidation validators.</summary>
    public List<Assembly> HandlerAssemblies { get; } = [];
}
