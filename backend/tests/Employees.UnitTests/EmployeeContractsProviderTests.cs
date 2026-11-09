using AwesomeAssertions;
using Employees.Application;
using Employees.Contracts;
using Employees.Domain;
using NSubstitute;

namespace Employees.UnitTests;

public sealed class EmployeeContractsProviderTests
{
    private readonly IEmployeeRepository _employees = Substitute.For<IEmployeeRepository>();
    private EmployeeContractsProvider Provider => new(_employees);

    [Fact]
    public async Task Payroll_profile_carries_salary_items_and_activity()
    {
        var employee = TestData.Employee(status: EmployeeStatus.Inactive);
        _employees.FindAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee);

        var profile = await Provider.GetAsync(employee.Id, TestContext.Current.CancellationToken);

        profile.Should().BeEquivalentTo(new EmployeePayrollProfile(
            employee.Id, "Sara Ahmed", "Engineering", "Engineer", new DateOnly(2020, 1, 15), false, 5000m,
            [new PayComponent("Transport", 500m)], [new PayComponent("Union dues", 50m)]));
    }

    [Fact]
    public async Task Payroll_profile_of_a_missing_employee_is_null() =>
        (await Provider.GetAsync(Guid.NewGuid(), TestContext.Current.CancellationToken)).Should().BeNull();

    [Fact]
    public async Task Directory_normalizes_the_email_before_looking_up()
    {
        var id = Guid.NewGuid();
        _employees.FindIdByEmailAsync("omar.hassan@acme.test", Arg.Any<CancellationToken>()).Returns(id);

        (await Provider.FindIdByEmailAsync("  Omar.Hassan@ACME.test ", TestContext.Current.CancellationToken)).Should().Be(id);
    }
}
