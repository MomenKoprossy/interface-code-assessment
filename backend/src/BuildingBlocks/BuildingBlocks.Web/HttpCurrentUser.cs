using System.Security.Claims;
using BuildingBlocks.Abstractions;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Web;

/// <summary>
/// Ambient identity for the current request, read from the JWT claims on <see cref="IHttpContextAccessor"/>.
/// Scoped — never static. Id from <c>sub</c>/<c>nameidentifier</c>, permissions from all <c>permission</c> claims.
/// </summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public Guid? Id
    {
        get
        {
            var value = User?.FindFirstValue("sub") ?? User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? UserName =>
        User?.FindFirstValue("name") ?? User?.FindFirstValue(ClaimTypes.Name);

    public IReadOnlyCollection<string> Permissions =>
        User?.FindAll("permission").Select(c => c.Value).ToArray() ?? [];

    public bool HasPermission(string permission) =>
        Permissions.Contains(permission) || Permissions.Contains("*");

    public string? FindClaim(string claimType) => User?.FindFirstValue(claimType);
}
