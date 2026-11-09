using AwesomeAssertions;
using BuildingBlocks.Abstractions;
using Identity.Application;
using Identity.Domain;
using NSubstitute;

namespace Identity.UnitTests.Application;

public class LoginTests
{
    private const string Password = "Correct-Horse-1";
    private static readonly DateTime ExpiresAt = new(2026, 11, 9, 13, 0, 0, DateTimeKind.Utc);

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenIssuer _tokens = Substitute.For<ITokenIssuer>();
    private readonly User _sara = User.CreateEmployee("sara.ahmed@acme.test", "Sara Ahmed", "stored-hash", Guid.NewGuid());

    public LoginTests()
    {
        _users.FindByEmailAsync("sara.ahmed@acme.test", Arg.Any<CancellationToken>()).Returns(_sara);
        _hasher.Verify("stored-hash", Password).Returns(true);
        _tokens.Issue(_sara).Returns(new AccessToken("jwt", ExpiresAt));
    }

    private ValueTask<Result<LoginResponse>> Login(string email, string password) =>
        new LoginHandler(_users, _hasher, _tokens).Handle(new LoginCommand(email, password), CancellationToken.None);

    [Fact]
    public async Task Valid_credentials_return_token_and_current_user()
    {
        var result = await Login(" Sara.Ahmed@ACME.test ", Password);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("jwt");
        result.Value.ExpiresAtUtc.Should().Be(ExpiresAt);
        result.Value.User.Should().BeEquivalentTo(new CurrentUserDto(
            _sara.Id, "sara.ahmed@acme.test", "Sara Ahmed", Role.Employee, _sara.EmployeeId, ["payroll.read.own"]));
    }

    [Theory]
    [InlineData("nobody@acme.test", Password)]      // unknown email
    [InlineData("sara.ahmed@acme.test", "wrong")]    // wrong password
    public async Task Bad_credentials_return_the_same_generic_401(string email, string password)
    {
        var result = await Login(email, password);

        result.IsFailure.Should().BeTrue();
        result.Error!.Value.Type.Should().Be(ErrorType.Unauthorized);
        result.Error!.Value.Message.Should().Be("Invalid email or password.");
        _tokens.DidNotReceiveWithAnyArgs().Issue(default!);
    }

    [Fact]
    public async Task Unknown_email_still_runs_a_dummy_password_verification()
    {
        await Login("nobody@acme.test", Password);

        _hasher.Received(1).Verify(null, Password);
    }
}
