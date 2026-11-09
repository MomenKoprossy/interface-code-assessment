using System.Reflection;

namespace Identity;

/// <summary>Assembly handle used by the host to register this module's handlers and validators.</summary>
public static class IdentityAssembly
{
    public static readonly Assembly Reference = typeof(IdentityAssembly).Assembly;
}
