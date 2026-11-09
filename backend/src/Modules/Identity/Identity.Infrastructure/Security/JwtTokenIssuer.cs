using System.Security.Claims;
using System.Text;
using BuildingBlocks.Abstractions;
using Identity.Application;
using Identity.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Infrastructure.Security;

/// <summary>HS256 access tokens with raw claim types (sub, name, email, role, employee_id, permission).</summary>
internal sealed class JwtTokenIssuer(IOptions<JwtOptions> options, IClock clock) : ITokenIssuer
{
    public const string EmployeeIdClaim = "employee_id";
    public const string PermissionClaim = "permission";

    private static readonly JsonWebTokenHandler Handler = new();

    public AccessToken Issue(User user)
    {
        var jwt = options.Value;
        var issuedAt = clock.UtcNow;
        var expiresAt = issuedAt.AddMinutes(jwt.AccessTokenLifetimeMinutes);

        var token = Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity(ClaimsOf(user)),
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)), SecurityAlgorithms.HmacSha256),
        });

        return new AccessToken(token, expiresAt);
    }

    private static IEnumerable<Claim> ClaimsOf(User user)
    {
        yield return new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString());
        yield return new Claim("name", user.DisplayName);
        yield return new Claim(JwtRegisteredClaimNames.Email, user.Email);
        yield return new Claim("role", user.Role.ToString());

        if (user.EmployeeId is { } employeeId)
            yield return new Claim(EmployeeIdClaim, employeeId.ToString());

        foreach (var permission in user.Permissions)
            yield return new Claim(PermissionClaim, permission);
    }
}
