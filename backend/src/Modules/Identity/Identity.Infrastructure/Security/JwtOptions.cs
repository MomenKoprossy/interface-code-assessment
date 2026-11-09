using System.Text;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Security;

/// <summary>
/// Section <c>Jwt</c>. The same section configures token validation in BuildingBlocks.Web (SecurityRegistration),
/// so issued tokens and accepted tokens always agree. <see cref="Key"/> comes only from the environment (<c>Jwt__Key</c>).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumKeyBytes = 32;

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int AccessTokenLifetimeMinutes { get; set; } = 60;
}

/// <summary>Fails startup on unusable JWT settings. Messages never include the key itself.</summary>
internal sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Key))
            failures.Add($"Jwt:Key is not set. Set the Jwt__Key environment variable to a random secret of at least {JwtOptions.MinimumKeyBytes} bytes.");
        else if (Encoding.UTF8.GetByteCount(options.Key) < JwtOptions.MinimumKeyBytes)
            failures.Add($"Jwt:Key is too short for HS256. Set the Jwt__Key environment variable to a random secret of at least {JwtOptions.MinimumKeyBytes} bytes.");

        if (string.IsNullOrWhiteSpace(options.Issuer))
            failures.Add("Jwt:Issuer is required.");
        if (string.IsNullOrWhiteSpace(options.Audience))
            failures.Add("Jwt:Audience is required.");
        if (options.AccessTokenLifetimeMinutes is < 1 or > 1440)
            failures.Add("Jwt:AccessTokenLifetimeMinutes must be between 1 and 1440.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
