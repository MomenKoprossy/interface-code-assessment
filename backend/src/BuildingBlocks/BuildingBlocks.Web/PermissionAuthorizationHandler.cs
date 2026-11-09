using Microsoft.AspNetCore.Authorization;

namespace BuildingBlocks.Web;

/// <summary>
/// Grants a <see cref="PermissionRequirement"/> when the user is SuperAdmin or holds the wildcard
/// <c>permission=*</c> claim (short-circuit for future permissions, zero data migration), otherwise
/// when the user holds the exact <c>permission</c> claim (§13.2).
/// </summary>
internal sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.IsInRole("SuperAdmin")
            || context.User.HasClaim("permission", "*")
            || context.User.HasClaim("permission", requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
