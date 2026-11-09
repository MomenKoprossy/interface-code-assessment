using Microsoft.AspNetCore.Builder;

namespace BuildingBlocks.Web;

/// <summary>Endpoint helpers for permission-based authorization.</summary>
public static class EndpointConventionBuilderExtensions
{
    /// <summary>
    /// Requires the given permission on the endpoint — shorthand for
    /// <c>RequireAuthorization("perm:" + permission)</c>, resolved by <see cref="PermissionPolicyProvider"/>.
    /// </summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(HasPermissionAttribute.PolicyPrefix + permission);
}
