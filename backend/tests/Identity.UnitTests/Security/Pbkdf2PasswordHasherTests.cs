using AwesomeAssertions;
using Identity.Infrastructure.Security;

namespace Identity.UnitTests.Security;

public class Pbkdf2PasswordHasherTests
{
    private static readonly Pbkdf2PasswordHasher Hasher = new();

    [Fact]
    public void Hash_round_trips_and_rejects_a_wrong_password()
    {
        var hash = Hasher.Hash("Correct-Horse-1");

        hash.Should().NotContain("Correct-Horse-1");
        Hasher.Verify(hash, "Correct-Horse-1").Should().BeTrue();
        Hasher.Verify(hash, "correct-horse-1").Should().BeFalse();
    }

    [Fact]
    public void Same_password_hashes_differently_each_time() =>
        Hasher.Hash("Correct-Horse-1").Should().NotBe(Hasher.Hash("Correct-Horse-1"));

    [Fact]
    public void Null_hash_never_verifies() =>
        Hasher.Verify(null, "anything").Should().BeFalse();
}
