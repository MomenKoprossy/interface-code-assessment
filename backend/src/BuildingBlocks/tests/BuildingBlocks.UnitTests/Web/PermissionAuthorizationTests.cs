using System.Security.Claims;
using AwesomeAssertions;
using BuildingBlocks.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.UnitTests.Web;

/// <summary>A <c>perm:</c> policy succeeds only for the exact permission claim: no super-admin role, no wildcard.</summary>
public class PermissionAuthorizationTests
{
    private const string Policy = HasPermissionAttribute.PolicyPrefix + "employees.read";

    private static async Task<bool> AuthorizeAsync(params Claim[] claims)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddPlatformSecurity(new ConfigurationBuilder().Build());
        await using var provider = services.BuildServiceProvider();

        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test", "name", "role"));
        var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, Policy);
        return result.Succeeded;
    }

    [Fact]
    public async Task Exact_permission_claim_is_granted() =>
        (await AuthorizeAsync(new Claim("permission", "employees.read"))).Should().BeTrue();

    [Fact]
    public async Task Other_permission_is_denied() =>
        (await AuthorizeAsync(new Claim("permission", "employees.write"))).Should().BeFalse();

    [Fact]
    public async Task Wildcard_permission_is_not_a_bypass() =>
        (await AuthorizeAsync(new Claim("permission", "*"))).Should().BeFalse();

    [Fact]
    public async Task SuperAdmin_role_is_not_a_bypass() =>
        (await AuthorizeAsync(new Claim("role", "SuperAdmin"))).Should().BeFalse();
}
