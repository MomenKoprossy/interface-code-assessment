using AwesomeAssertions;
using Identity.Domain;

namespace Identity.UnitTests.Domain;

public class UserTests
{
    [Fact]
    public void Hr_role_has_employee_management_and_all_payroll_permissions() =>
        RolePermissions.For(Role.HR).Should().BeEquivalentTo("employees.read", "employees.write", "payroll.read.all");

    [Fact]
    public void Employee_role_can_only_read_own_payroll() =>
        RolePermissions.For(Role.Employee).Should().Equal("payroll.read.own");

    [Fact]
    public void Email_is_trimmed_and_lower_cased()
    {
        var user = User.CreateHr("  HR@Acme.TEST ", " Hana Rashed ", "hash");

        user.Email.Should().Be("hr@acme.test");
        user.DisplayName.Should().Be("Hana Rashed");
    }

    [Fact]
    public void Hr_user_is_not_linked_to_an_employee()
    {
        var user = User.CreateHr("hr@acme.test", "Hana Rashed", "hash");

        user.Role.Should().Be(Role.HR);
        user.EmployeeId.Should().BeNull();
        user.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Employee_user_is_linked_to_its_employee()
    {
        var employeeId = Guid.NewGuid();

        var user = User.CreateEmployee("sara.ahmed@acme.test", "Sara Ahmed", "hash", employeeId);

        user.Role.Should().Be(Role.Employee);
        user.EmployeeId.Should().Be(employeeId);
        user.Permissions.Should().Equal("payroll.read.own");
    }

    [Fact]
    public void Employee_user_without_an_employee_is_rejected()
    {
        var create = () => User.CreateEmployee("sara.ahmed@acme.test", "Sara Ahmed", "hash", Guid.Empty);

        create.Should().Throw<ArgumentException>();
    }
}
