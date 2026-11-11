using Microsoft.AspNetCore.Authorization;

namespace BuildingBlocks.Web;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
