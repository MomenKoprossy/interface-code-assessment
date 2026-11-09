using Identity.Domain;

namespace Identity.Application;

public interface IUserRepository
{
    /// <param name="normalizedEmail">Already trimmed and lower-cased (<see cref="User.NormalizeEmail"/>).</param>
    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);
    Task<User?> FindAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(string normalizedEmail, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
}

public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// True when <paramref name="password"/> matches <paramref name="passwordHash"/>. A null hash (unknown user) is
    /// verified against a dummy hash so the call costs the same, then returns false (timing equalization).
    /// </summary>
    bool Verify(string? passwordHash, string password);
}

public sealed record AccessToken(string Value, DateTime ExpiresAtUtc);

public interface ITokenIssuer
{
    AccessToken Issue(User user);
}
