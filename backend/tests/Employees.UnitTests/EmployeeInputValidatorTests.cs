using AwesomeAssertions;
using Employees.Application;
using FluentValidation.Results;

namespace Employees.UnitTests;

public sealed class EmployeeInputValidatorTests
{
    private readonly CreateEmployeeValidator _validator = new(TestData.PinnedClock());

    private string[] ErrorsFor(CreateEmployeeCommand command, string property) =>
        Validate(command).Errors.Where(e => e.PropertyName == property).Select(e => e.ErrorMessage).ToArray();

    private ValidationResult Validate(CreateEmployeeCommand command) => _validator.Validate(command);

    [Fact]
    public void Valid_input_passes() => Validate(TestData.ValidCreate()).IsValid.Should().BeTrue();

    [Fact]
    public void Empty_pay_item_lists_are_allowed() =>
        Validate(TestData.ValidCreate() with { Allowances = [], Deductions = null }).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(null, "Full name is required.")]
    [InlineData("", "Full name is required.")]
    [InlineData("   ", "Full name is required.")]
    public void Full_name_is_required(string? fullName, string expected) =>
        ErrorsFor(TestData.ValidCreate() with { FullName = fullName }, "FullName").Should().Equal(expected);

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Full_name_max_100(int length, bool valid) =>
        ErrorsFor(TestData.ValidCreate() with { FullName = new string('a', length) }, "FullName").Should().HaveCount(valid ? 0 : 1);

    [Fact]
    public void Full_name_length_is_measured_after_trimming() =>
        ErrorsFor(TestData.ValidCreate() with { FullName = "  " + new string('a', 100) + "  " }, "FullName").Should().BeEmpty();

    [Theory]
    [InlineData(nameof(EmployeeInput.JobTitle), 100, true)]
    [InlineData(nameof(EmployeeInput.JobTitle), 101, false)]
    [InlineData(nameof(EmployeeInput.Department), 100, true)]
    [InlineData(nameof(EmployeeInput.Department), 101, false)]
    public void Job_title_and_department_max_100(string property, int length, bool valid)
    {
        var text = new string('a', length);
        var command = property == nameof(EmployeeInput.JobTitle)
            ? TestData.ValidCreate() with { JobTitle = text }
            : TestData.ValidCreate() with { Department = text };

        ErrorsFor(command, property).Should().HaveCount(valid ? 0 : 1);
    }

    [Theory]
    [InlineData(nameof(EmployeeInput.JobTitle), "Job title is required.")]
    [InlineData(nameof(EmployeeInput.Department), "Department is required.")]
    public void Job_title_and_department_are_required(string property, string expected)
    {
        var command = property == nameof(EmployeeInput.JobTitle)
            ? TestData.ValidCreate() with { JobTitle = " " }
            : TestData.ValidCreate() with { Department = null };

        ErrorsFor(command, property).Should().Equal(expected);
    }

    [Theory]
    [InlineData("a@b.co")]
    [InlineData("first.last+tag@sub.domain.org")]
    [InlineData("  Padded@Acme.Test  ")]
    public void Valid_emails(string email) =>
        ErrorsFor(TestData.ValidCreate() with { Email = email }, "Email").Should().BeEmpty();

    [Theory]
    [InlineData("plainaddress")]
    [InlineData("no-tld@domain")]
    [InlineData("@acme.test")]
    [InlineData("two@@acme.test")]
    [InlineData("with space@acme.test")]
    [InlineData("trailing.dot@acme.")]
    [InlineData("double.dot@acme..test")]
    public void Invalid_emails(string email) =>
        ErrorsFor(TestData.ValidCreate() with { Email = email }, "Email").Should().Equal("Email must be a valid email address.");

    [Fact]
    public void Email_is_required() =>
        ErrorsFor(TestData.ValidCreate() with { Email = "" }, "Email").Should().Equal("Email is required.");

    [Theory]
    [InlineData(254, true)]
    [InlineData(255, false)]
    public void Email_max_254(int length, bool valid)
    {
        const string domain = "@acme.test";
        var email = new string('a', length - domain.Length) + domain;
        ErrorsFor(TestData.ValidCreate() with { Email = email }, "Email").Should().HaveCount(valid ? 0 : 1);
    }

    [Fact]
    public void Hire_date_today_utc_is_allowed() =>
        ErrorsFor(TestData.ValidCreate() with { HireDate = TestData.Today }, "HireDate").Should().BeEmpty();

    [Fact]
    public void Hire_date_tomorrow_is_rejected() =>
        ErrorsFor(TestData.ValidCreate() with { HireDate = TestData.Today.AddDays(1) }, "HireDate")
            .Should().Equal("Hire date cannot be in the future.");

    [Fact]
    public void Hire_date_1900_01_01_is_allowed() =>
        ErrorsFor(TestData.ValidCreate() with { HireDate = new DateOnly(1900, 1, 1) }, "HireDate").Should().BeEmpty();

    [Fact]
    public void Hire_date_1899_12_31_is_rejected() =>
        ErrorsFor(TestData.ValidCreate() with { HireDate = new DateOnly(1899, 12, 31) }, "HireDate")
            .Should().Equal("Hire date cannot be before 1900-01-01.");

    [Fact]
    public void Hire_date_is_required() =>
        ErrorsFor(TestData.ValidCreate() with { HireDate = null }, "HireDate").Should().Equal("Hire date is required.");

    [Theory]
    [InlineData("0.01", null)]
    [InlineData("1000000", null)]
    [InlineData("1000000.00", null)]
    [InlineData("12.50", null)]
    [InlineData("12.500", null)]
    [InlineData("0", "Base monthly salary must be greater than 0.")]
    [InlineData("-1", "Base monthly salary must be greater than 0.")]
    [InlineData("1000000.01", "Base monthly salary must be at most 1,000,000.")]
    [InlineData("12.345", "Base monthly salary must have at most 2 decimal places.")]
    public void Salary_boundaries(string salary, string? expected) =>
        ErrorsFor(TestData.ValidCreate() with { BaseMonthlySalary = decimal.Parse(salary, System.Globalization.CultureInfo.InvariantCulture) },
                "BaseMonthlySalary")
            .Should().Equal(expected is null ? [] : [expected]);

    [Fact]
    public void Salary_is_required() =>
        ErrorsFor(TestData.ValidCreate() with { BaseMonthlySalary = null }, "BaseMonthlySalary")
            .Should().Equal("Base monthly salary is required.");

    [Theory]
    [InlineData("Active", null)]
    [InlineData("Inactive", null)]
    [InlineData(null, "Status is required.")]
    [InlineData("", "Status is required.")]
    [InlineData("Retired", "Status must be Active or Inactive.")]
    [InlineData("active", "Status must be Active or Inactive.")]
    [InlineData("1", "Status must be Active or Inactive.")]
    public void Status_must_be_active_or_inactive(string? status, string? expected) =>
        ErrorsFor(TestData.ValidCreate() with { Status = status }, "Status").Should().Equal(expected is null ? [] : [expected]);

    [Theory]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void At_most_20_allowances(int count, bool valid)
    {
        var items = Enumerable.Range(1, count).Select(i => new PayItemInput($"Item {i}", 1m)).ToList();
        ErrorsFor(TestData.ValidCreate() with { Allowances = items }, "Allowances")
            .Should().Equal(valid ? [] : ["At most 20 allowances are allowed."]);
    }

    [Theory]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void At_most_20_deductions(int count, bool valid)
    {
        var items = Enumerable.Range(1, count).Select(i => new PayItemInput($"Item {i}", 1m)).ToList();
        ErrorsFor(TestData.ValidCreate() with { Deductions = items }, "Deductions")
            .Should().Equal(valid ? [] : ["At most 20 deductions are allowed."]);
    }

    [Theory]
    [InlineData(null, "Name is required.")]
    [InlineData("  ", "Name is required.")]
    [InlineData("12345678901234567890123456789012345678901234567890X", "Name must be at most 50 characters.")]
    public void Pay_item_name_rules(string? name, string expected) =>
        ErrorsFor(TestData.ValidCreate() with { Allowances = [new PayItemInput("Ok", 1m), new PayItemInput(name, 1m)] },
                "Allowances[1].Name")
            .Should().Equal(expected);

    [Fact]
    public void Pay_item_name_of_50_is_allowed() =>
        Validate(TestData.ValidCreate() with { Deductions = [new PayItemInput(new string('n', 50), 1m)] }).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("0.01", null)]
    [InlineData("1000000", null)]
    [InlineData(null, "Amount is required.")]
    [InlineData("0", "Amount must be greater than 0.")]
    [InlineData("1000000.01", "Amount must be at most 1,000,000.")]
    [InlineData("1.005", "Amount must have at most 2 decimal places.")]
    public void Pay_item_amount_rules(string? amount, string? expected)
    {
        decimal? value = amount is null ? null : decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);
        ErrorsFor(TestData.ValidCreate() with { Deductions = [new PayItemInput("Loan", value)] }, "Deductions[0].Amount")
            .Should().Equal(expected is null ? [] : [expected]);
    }

    [Fact]
    public void Pay_item_names_must_be_unique_case_insensitively_after_trimming()
    {
        var command = TestData.ValidCreate() with
        {
            Allowances = [new PayItemInput("Transport", 1m), new PayItemInput("Meal", 1m), new PayItemInput(" TRANSPORT ", 2m)],
        };

        ErrorsFor(command, "Allowances[2].Name").Should().Equal("Name must be unique within the list.");
        Validate(command).Errors.Should().ContainSingle();
    }

    [Fact]
    public void Same_name_in_allowances_and_deductions_is_allowed() =>
        Validate(TestData.ValidCreate() with
        {
            Allowances = [new PayItemInput("Transport", 1m)],
            Deductions = [new PayItemInput("Transport", 1m)],
        }).IsValid.Should().BeTrue();

    [Fact]
    public void Null_pay_item_is_rejected() =>
        ErrorsFor(TestData.ValidCreate() with { Allowances = [null!] }, "Allowances[0]").Should().Equal("Item is required.");

    [Fact]
    public void Update_uses_the_same_rules()
    {
        var update = new UpdateEmployeeValidator(TestData.PinnedClock());
        var result = update.Validate(new UpdateEmployeeCommand { Id = Guid.NewGuid() });

        result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(
            "FullName", "Email", "JobTitle", "Department", "HireDate", "BaseMonthlySalary", "Status");
    }
}
