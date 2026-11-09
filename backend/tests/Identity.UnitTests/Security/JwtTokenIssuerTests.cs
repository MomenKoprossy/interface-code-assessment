using System.Security.Claims;
using AwesomeAssertions;
using BuildingBlocks.Abstractions;
using BuildingBlocks.Web;
using Identity.Domain;
using Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;

namespace Identity.UnitTests.Security;

public class JwtTokenIssuerTests
{
    private const string Issuer = "employee-payroll-api";
    private const string Audience = "employee-payroll-web";
    private const string Key = "unit-test-signing-key-0123456789abcdef";
    private static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    private static readonly JwtTokenIssuer Issuer60Minutes = new(
        Options.Create(new JwtOptions { Issuer = Issuer, Audience = Audience, Key = Key, AccessTokenLifetimeMinutes = 60 }),
        PinnedClock(Now));

    private static IClock PinnedClock(DateTime utcNow)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(utcNow);
        return clock;
    }

    [Fact]
    public void Hr_token_carries_identity_role_and_hr_permissions_but_no_employee_id()
    {
        var hr = User.CreateHr("hr@acme.test", "Hana Rashed", "hash");

        var token = new JsonWebToken(Issuer60Minutes.Issue(hr).Value);

        token.Subject.Should().Be(hr.Id.ToString());
        token.GetClaim("name").Value.Should().Be("Hana Rashed");
        token.GetClaim("email").Value.Should().Be("hr@acme.test");
        token.GetClaim("role").Value.Should().Be("HR");
        token.TryGetClaim("employee_id", out _).Should().BeFalse();
        token.Claims.Where(c => c.Type == "permission").Select(c => c.Value)
            .Should().BeEquivalentTo("employees.read", "employees.write", "payroll.read.all");
    }

    [Fact]
    public void Employee_token_carries_employee_id_and_own_payroll_permission()
    {
        var employeeId = Guid.NewGuid();
        var sara = User.CreateEmployee("sara.ahmed@acme.test", "Sara Ahmed", "hash", employeeId);

        var token = new JsonWebToken(Issuer60Minutes.Issue(sara).Value);

        token.Subject.Should().Be(sara.Id.ToString());
        token.GetClaim("role").Value.Should().Be("Employee");
        token.GetClaim("employee_id").Value.Should().Be(employeeId.ToString());
        token.Claims.Where(c => c.Type == "permission").Select(c => c.Value).Should().Equal("payroll.read.own");
    }

    [Fact]
    public void Token_is_hs256_and_expires_after_the_configured_lifetime_from_the_clock()
    {
        var issued = Issuer60Minutes.Issue(User.CreateHr("hr@acme.test", "Hana Rashed", "hash"));
        var token = new JsonWebToken(issued.Value);

        token.Alg.Should().Be(SecurityAlgorithms.HmacSha256);
        token.IssuedAt.Should().Be(Now);
        token.ValidTo.Should().Be(Now.AddMinutes(60));
        issued.ExpiresAtUtc.Should().Be(Now.AddMinutes(60));
    }

    [Theory]
    [InlineData(0, true)]          // just issued
    [InlineData(59, true)]         // within lifetime
    [InlineData(61 + 5, false)]    // past expiry plus the default 5-minute clock skew
    public async Task Token_validates_with_the_platform_bearer_parameters(int minutesLater, bool expectedValid)
    {
        var sara = User.CreateEmployee("sara.ahmed@acme.test", "Sara Ahmed", "hash", Guid.NewGuid());
        var token = Issuer60Minutes.Issue(sara).Value;

        var parameters = PlatformValidationParameters();
        PinLifetimeCheck(parameters, Now.AddMinutes(minutesLater));
        var result = await new JsonWebTokenHandler { MapInboundClaims = false }.ValidateTokenAsync(token, parameters);

        result.IsValid.Should().Be(expectedValid);
        if (expectedValid)
        {
            new ClaimsPrincipal(result.ClaimsIdentity).IsInRole("Employee").Should().BeTrue();
            result.ClaimsIdentity.Name.Should().Be("Sara Ahmed");
        }
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        var otherIssuer = new JwtTokenIssuer(
            Options.Create(new JwtOptions { Issuer = Issuer, Audience = Audience, Key = new string('x', 32), AccessTokenLifetimeMinutes = 60 }),
            PinnedClock(Now));
        var token = otherIssuer.Issue(User.CreateHr("hr@acme.test", "Hana Rashed", "hash")).Value;

        var parameters = PlatformValidationParameters();
        PinLifetimeCheck(parameters, Now);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);

        result.IsValid.Should().BeFalse();
    }

    /// <summary>The exact parameters BuildingBlocks.Web configures for the API, built from the same "Jwt" section.</summary>
    private static TokenValidationParameters PlatformValidationParameters()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
                ["Jwt:Key"] = Key,
            })
            .Build();

        using var provider = new ServiceCollection().AddLogging().AddPlatformSecurity(configuration).BuildServiceProvider();
        var bearer = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);

        bearer.MapInboundClaims.Should().BeFalse();
        return bearer.TokenValidationParameters.Clone();
    }

    /// <summary>
    /// Same lifetime rule as the default validator (with the platform's clock skew), evaluated at a pinned instant
    /// instead of the real clock. TokenValidationParameters.TimeProvider is not public in this IdentityModel version.
    /// </summary>
    private static void PinLifetimeCheck(TokenValidationParameters parameters, DateTime utcNow)
    {
        var skew = parameters.ClockSkew;
        parameters.LifetimeValidator = (notBefore, expires, _, _) =>
            (notBefore is null || notBefore <= utcNow + skew) && expires is { } e && e > utcNow - skew;
    }
}
