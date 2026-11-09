using AwesomeAssertions;
using Identity.Application;

namespace Identity.UnitTests.Application;

public class LoginValidatorTests
{
    private static readonly LoginValidator Validator = new();

    private static IEnumerable<string> ErrorsFor(string email, string password, string property) =>
        Validator.Validate(new LoginCommand(email, password)).Errors
            .Where(e => e.PropertyName == property)
            .Select(e => e.ErrorMessage);

    [Theory]
    [InlineData("hr@acme.test")]
    [InlineData("a@b.c")]
    public void Valid_email_passes(string email) =>
        ErrorsFor(email, "pw", "Email").Should().BeEmpty();

    [Theory]
    [InlineData(null, "Email is required.")]
    [InlineData("", "Email is required.")]
    [InlineData("   ", "Email is required.")]
    [InlineData("hr", "Email must be a valid email address.")]
    [InlineData("hr@acme", "Email must be a valid email address.")]
    [InlineData("h r@acme.test", "Email must be a valid email address.")]
    public void Invalid_email_fails(string? email, string expected) =>
        ErrorsFor(email!, "pw", "Email").Should().Equal(expected);

    [Fact]
    public void Email_at_max_length_passes() =>
        ErrorsFor(EmailOfLength(254), "pw", "Email").Should().BeEmpty();

    [Fact]
    public void Email_over_max_length_fails() =>
        ErrorsFor(EmailOfLength(255), "pw", "Email").Should().Equal("Email must be at most 254 characters.");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Missing_password_fails(string? password) =>
        ErrorsFor("hr@acme.test", password!, "Password").Should().Equal("Password is required.");

    [Fact]
    public void Password_at_max_length_passes() =>
        ErrorsFor("hr@acme.test", new string('p', 128), "Password").Should().BeEmpty();

    [Fact]
    public void Password_over_max_length_fails() =>
        ErrorsFor("hr@acme.test", new string('p', 129), "Password").Should().Equal("Password must be at most 128 characters.");

    private static string EmailOfLength(int length) => new string('a', length - "@acme.test".Length) + "@acme.test";
}
