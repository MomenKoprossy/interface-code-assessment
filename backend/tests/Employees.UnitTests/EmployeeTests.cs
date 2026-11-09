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
            EmployeeStatus.Active, [new PayItem(" Transport ", 500m)], [new PayItem("Loan ", 50m)]), TestData.Today);

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

        employee.Update(TestData.Data() with { Allowances = [new PayItem("Housing", 1500m), new PayItem("Meal", 300m)], Deductions = [] }, TestData.Today);

        employee.Allowances.Should().Equal(new PayItem("Housing", 1500m), new PayItem("Meal", 300m));
        employee.Deductions.Should().BeEmpty();
    }

    [Fact]
    public void Deactivating_raises_EmployeeDeactivated()
    {
        var employee = TestData.Employee();

        employee.Update(TestData.Data(status: EmployeeStatus.Inactive), TestData.Today);

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

        employee.Update(TestData.Data(status: to), TestData.Today);

        employee.DomainEvents.Should().BeEmpty();
    }
}

/// <summary>
/// The aggregate guards its invariants even when a caller skips the application validator (seeder, another module,
/// a test). Each case breaks exactly one rule of <see cref="EmployeeRules"/>, and the guard must name that field.
/// </summary>
public sealed class EmployeeInvariantTests
{
    public static TheoryData<string, EmployeeData> Violations()
    {
        var valid = TestData.Data();
        var tooLong = new string('x', EmployeeRules.MaxTextLength + 1);
        return new()
        {
            { "FullName", valid with { FullName = "   " } },
            { "FullName", valid with { FullName = tooLong } },
            { "Email", valid with { Email = "not-an-email" } },
            { "Email", valid with { Email = "a@b..c" } },
            { "JobTitle", valid with { JobTitle = "" } },
            { "Department", valid with { Department = tooLong } },
            { "HireDate", valid with { HireDate = TestData.Today.AddDays(1) } },
            { "HireDate", valid with { HireDate = new DateOnly(1899, 12, 31) } },
            { "BaseMonthlySalary", valid with { BaseMonthlySalary = 0m } },
            { "BaseMonthlySalary", valid with { BaseMonthlySalary = -100m } },
            { "BaseMonthlySalary", valid with { BaseMonthlySalary = 10.005m } },
            { "BaseMonthlySalary", valid with { BaseMonthlySalary = 1_000_000.01m } },
            { "Status", valid with { Status = (EmployeeStatus)42 } },
            { "Allowances", valid with { Allowances = [new PayItem("Transport", 0m)] } },
            { "Allowances", valid with { Allowances = [new PayItem(" ", 10m)] } },
            { "Allowances", valid with { Allowances = [new PayItem("Meal", 10m), new PayItem(" meal ", 20m)] } },
            { "Deductions", valid with { Deductions = [.. Enumerable.Range(1, EmployeeRules.MaxPayItems + 1).Select(i => new PayItem($"D{i}", 1m))] } },
        };
    }

    [Theory]
    [MemberData(nameof(Violations))]
    public void Create_refuses_data_that_breaks_an_invariant(string field, EmployeeData data)
    {
        var act = () => Employee.Create(data, TestData.Today);

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be(field);
    }

    [Theory]
    [MemberData(nameof(Violations))]
    public void Update_refuses_it_and_leaves_the_employee_unchanged(string field, EmployeeData data)
    {
        var employee = TestData.Employee();
        var before = (employee.FullName, employee.Email, employee.BaseMonthlySalary, employee.HireDate, employee.Allowances, employee.Deductions);

        var act = () => employee.Update(data, TestData.Today);

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be(field);
        (employee.FullName, employee.Email, employee.BaseMonthlySalary, employee.HireDate, employee.Allowances, employee.Deductions)
            .Should().Be(before);
    }

    [Fact]
    public void Boundary_values_are_accepted()
    {
        var atLimits = TestData.Data() with
        {
            FullName = new string('x', EmployeeRules.MaxTextLength),
            HireDate = TestData.Today,
            BaseMonthlySalary = EmployeeRules.MaxAmount,
            Allowances = [new PayItem(new string('a', EmployeeRules.MaxPayItemNameLength), 0.01m)],
            Deductions = [.. Enumerable.Range(1, EmployeeRules.MaxPayItems).Select(i => new PayItem($"D{i}", 1m))],
        };

        var act = () => Employee.Create(atLimits, TestData.Today);

        act.Should().NotThrow();
    }

    [Fact]
    public void Hire_date_on_the_minimum_is_accepted() =>
        FluentActions.Invoking(() => Employee.Create(TestData.Data() with { HireDate = EmployeeRules.MinHireDate }, TestData.Today))
            .Should().NotThrow();
}
