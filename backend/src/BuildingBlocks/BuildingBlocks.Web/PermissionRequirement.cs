using Microsoft.AspNetCore.Authorization;

namespace BuildingBlocks.Web;

/// <summary>The single permission an endpoint requires; evaluated by <see cref="PermissionAuthorizationHandler"/>.</summary>
public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
