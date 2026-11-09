using AwesomeAssertions;
using BuildingBlocks.Abstractions;
using Identity.Application;
using Identity.Domain;
using NSubstitute;

namespace Identity.UnitTests.Application;

public class GetCurrentUserTests
{
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();

    private ValueTask<Result<CurrentUserDto>> Handle() =>
        new GetCurrentUserHandler(_currentUser, _users).Handle(new GetCurrentUserQuery(), CancellationToken.None);

    [Fact]
    public async Task Returns_the_signed_in_user_with_permissions()
    {
        var hr = User.CreateHr("hr@acme.test", "Hana Rashed", "hash");
        _currentUser.Id.Returns(hr.Id);
        _users.FindAsync(hr.Id, Arg.Any<CancellationToken>()).Returns(hr);

        var result = await Handle();

        result.Value.Should().BeEquivalentTo(new CurrentUserDto(
            hr.Id, "hr@acme.test", "Hana Rashed", Role.HR, null, ["employees.read", "employees.write", "payroll.read.all"]));
    }

    [Fact]
    public async Task No_user_id_is_unauthorized()
    {
        _currentUser.Id.Returns((Guid?)null);

        (await Handle()).Error!.Value.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task User_that_no_longer_exists_is_unauthorized()
    {
        _currentUser.Id.Returns(Guid.NewGuid());

        (await Handle()).Error!.Value.Type.Should().Be(ErrorType.Unauthorized);
    }
}
