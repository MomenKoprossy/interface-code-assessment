using AwesomeAssertions;
using Identity.Infrastructure.Security;

namespace Identity.UnitTests.Security;

public class JwtOptionsValidatorTests
{
    private static JwtOptions ValidOptions(string key) =>
        new() { Issuer = "api", Audience = "web", Key = key, AccessTokenLifetimeMinutes = 60 };

    [Fact]
    public void Key_of_exactly_32_bytes_is_accepted() =>
        new JwtOptionsValidator().Validate(null, ValidOptions(new string('k', 32))).Succeeded.Should().BeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_key_fails_and_tells_the_operator_to_set_Jwt__Key(string key)
    {
        var result = new JwtOptionsValidator().Validate(null, ValidOptions(key));

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Jwt__Key").And.Contain("not set");
    }

    [Fact]
    public void Key_of_31_bytes_fails_without_echoing_the_key()
    {
        var shortKey = "short-secret-key-of-31-bytes-xx";

        var result = new JwtOptionsValidator().Validate(null, ValidOptions(shortKey));

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Jwt__Key").And.Contain("at least 32 bytes").And.NotContain(shortKey);
    }

    [Fact]
    public void Key_length_is_measured_in_utf8_bytes_not_characters()
    {
        var sixteenTwoByteChars = new string('é', 16); // 16 chars, 32 UTF-8 bytes

        new JwtOptionsValidator().Validate(null, ValidOptions(sixteenTwoByteChars)).Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(1440, true)]
    [InlineData(1441, false)]
    public void Token_lifetime_must_be_between_1_minute_and_1_day(int minutes, bool valid)
    {
        var options = ValidOptions(new string('k', 32));
        options.AccessTokenLifetimeMinutes = minutes;

        new JwtOptionsValidator().Validate(null, options).Succeeded.Should().Be(valid);
    }

    [Fact]
    public void Issuer_and_audience_are_required()
    {
        var options = ValidOptions(new string('k', 32));
        options.Issuer = "";
        options.Audience = "";

        var result = new JwtOptionsValidator().Validate(null, options);

        result.Failures.Should().Contain("Jwt:Issuer is required.").And.Contain("Jwt:Audience is required.");
    }
}
