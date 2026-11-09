using Identity.Application;
using Identity.Domain;
using Microsoft.AspNetCore.Identity;

namespace Identity.Infrastructure.Security;

/// <summary>ASP.NET Core Identity's <see cref="PasswordHasher{TUser}"/> (PBKDF2, salted, versioned format).</summary>
internal sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();
    private readonly string _dummyHash;

    public Pbkdf2PasswordHasher() => _dummyHash = Hash(Guid.NewGuid().ToString());

    // The default hasher ignores the user argument.
    public string Hash(string password) => _hasher.HashPassword(null!, password);

    // ponytail: SuccessRehashNeeded counts as a match without rehashing; add a rehash-on-login when the iteration count changes.
    public bool Verify(string? passwordHash, string password)
    {
        var matches = _hasher.VerifyHashedPassword(null!, passwordHash ?? _dummyHash, password) != PasswordVerificationResult.Failed;
        return passwordHash is not null && matches;
    }
}
