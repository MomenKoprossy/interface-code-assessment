using BuildingBlocks.Abstractions;
using Microsoft.AspNetCore.Authorization;

namespace BuildingBlocks.Web;

/// <summary>Exact permission claim only: there is no super-admin role or wildcard permission.</summary>
internal sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim(PlatformClaimTypes.Permission, requirement.Permission))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
