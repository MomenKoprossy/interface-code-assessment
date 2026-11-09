using AwesomeAssertions;
using Employees.Contracts;
using Employees.Domain;

namespace Employees.UnitTests;

public sealed class EmployeeTests
{
    [Fact]
    public void Create_trims_text_and_lower_cases_email()
    {
        var employee = Employee.Create(new EmployeeData(
            "  Sara Ahmed ", "  Sara.Ahmed@ACME.Test ", " Engineer ", " Engineering ", new DateOnly(2020, 1, 15), 5000m,
            EmployeeStatus.Active, [new PayItem(" Transport ", 500m)], [new PayItem("Loan ", 50m)]));

        employee.FullName.Should().Be("Sara Ahmed");
        employee.Email.Should().Be("sara.ahmed@acme.test");
        employee.JobTitle.Should().Be("Engineer");
        employee.Department.Should().Be("Engineering");
        employee.Allowances.Should().Equal(new PayItem("Transport", 500m));
        employee.Deductions.Should().Equal(new PayItem("Loan", 50m));
        employee.Id.Should().NotBeEmpty();
        employee.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Create_inactive_raises_no_event() =>
        TestData.Employee(status: EmployeeStatus.Inactive).DomainEvents.Should().BeEmpty();

    [Fact]
    public void Update_replaces_the_pay_item_lists()
    {
        var employee = TestData.Employee();

        employee.Update(TestData.Data() with { Allowances = [new PayItem("Housing", 1500m), new PayItem("Meal", 300m)], Deductions = [] });

        employee.Allowances.Should().Equal(new PayItem("Housing", 1500m), new PayItem("Meal", 300m));
        employee.Deductions.Should().BeEmpty();
    }

    [Fact]
    public void Deactivating_raises_EmployeeDeactivated()
    {
        var employee = TestData.Employee();

        employee.Update(TestData.Data(status: EmployeeStatus.Inactive));

        employee.IsActive.Should().BeFalse();
        employee.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<EmployeeDeactivated>()
            .Which.EmployeeId.Should().Be(employee.Id);
    }

    [Theory]
    [InlineData(EmployeeStatus.Active, EmployeeStatus.Active)]
    [InlineData(EmployeeStatus.Inactive, EmployeeStatus.Inactive)]
    [InlineData(EmployeeStatus.Inactive, EmployeeStatus.Active)]
    public void Other_status_changes_raise_no_event(EmployeeStatus from, EmployeeStatus to)
    {
        var employee = TestData.Employee(status: from);

        employee.Update(TestData.Data(status: to));

        employee.DomainEvents.Should().BeEmpty();
    }
}
