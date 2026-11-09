using Microsoft.AspNetCore.Authorization;

namespace BuildingBlocks.Web;

/// <summary>
/// Declarative permission gate. Encodes the permission into the policy NAME (<c>perm:{permission}</c>);
/// <see cref="PermissionPolicyProvider"/> builds the matching policy on demand (§13.2).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "perm:";

    public HasPermissionAttribute(string permission) => Policy = PolicyPrefix + permission;

    public string Permission => Policy![PolicyPrefix.Length..];
}
