using AwesomeAssertions;
using BuildingBlocks.Abstractions;
using Employees.Contracts;
using Identity.Application;
using Identity.Domain;
using Identity.Infrastructure.Seeding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Identity.UnitTests.Seeding;

public class IdentitySeederTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IEmployeeDirectory _employees = Substitute.For<IEmployeeDirectory>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly List<User> _added = [];

    public IdentitySeederTests()
    {
        _hasher.Hash("demo-pass").Returns("demo-hash");
        _users.AddAsync(Arg.Do<User>(_added.Add), Arg.Any<CancellationToken>());
    }

    private Task Seed(string? demoPassword)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:DemoPassword"] = demoPassword })
            .Build();
        return new IdentitySeeder(configuration, _users, _hasher, _employees, _unitOfWork, NullLogger<IdentitySeeder>.Instance)
            .SeedAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Without_a_demo_password_nothing_is_seeded(string? password)
    {
        await Seed(password);

        _added.Should().BeEmpty();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Seeds_hr_and_employee_logins_linked_to_their_employees()
    {
        var saraId = Guid.NewGuid();
        var omarId = Guid.NewGuid();
        _employees.FindIdByEmailAsync("sara.ahmed@acme.test", Arg.Any<CancellationToken>()).Returns(saraId);
        _employees.FindIdByEmailAsync("omar.hassan@acme.test", Arg.Any<CancellationToken>()).Returns(omarId);

        await Seed("demo-pass");

        _added.Select(u => (u.Email, u.DisplayName, u.Role, u.EmployeeId, u.PasswordHash)).Should().Equal(
            ("hr@acme.test", "Hana Rashed", Role.HR, null, "demo-hash"),
            ("sara.ahmed@acme.test", "Sara Ahmed", Role.Employee, saraId, "demo-hash"),
            ("omar.hassan@acme.test", "Omar Hassan", Role.Employee, omarId, "demo-hash"));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Existing_logins_and_missing_employees_are_skipped()
    {
        _users.AnyAsync("hr@acme.test", Arg.Any<CancellationToken>()).Returns(true);
        _users.AnyAsync("sara.ahmed@acme.test", Arg.Any<CancellationToken>()).Returns(true);
        _employees.FindIdByEmailAsync("omar.hassan@acme.test", Arg.Any<CancellationToken>()).Returns((Guid?)null);

        await Seed("demo-pass");

        _added.Should().BeEmpty();
    }
}
