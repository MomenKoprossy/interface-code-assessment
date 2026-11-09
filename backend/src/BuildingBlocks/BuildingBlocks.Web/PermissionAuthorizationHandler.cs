using Microsoft.AspNetCore.Authorization;

namespace BuildingBlocks.Web;

/// <summary>
/// Grants a <see cref="PermissionRequirement"/> only when the user holds the exact <c>permission</c> claim.
/// There is no super-admin role or wildcard permission.
/// </summary>
internal sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim("permission", requirement.Permission))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
