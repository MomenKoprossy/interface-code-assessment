using BuildingBlocks.Abstractions;
using Employees.Application;
using Employees.Domain;
using NSubstitute;

namespace Employees.UnitTests;

internal static class TestData
{
    /// <summary>"Today, UTC" for every test: late in the day, so a local-time bug would show up as tomorrow.</summary>
    public static readonly DateOnly Today = new(2026, 11, 9);

    public static IClock PinnedClock()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(new DateTime(2026, 11, 9, 23, 30, 0, DateTimeKind.Utc));
        return clock;
    }

    public static CreateEmployeeCommand ValidCreate() => new()
    {
        FullName = "Sara Ahmed",
        Email = "sara.ahmed@acme.test",
        JobTitle = "Engineer",
        Department = "Engineering",
        HireDate = new DateOnly(2020, 1, 15),
        BaseMonthlySalary = 5000m,
        Status = "Active",
        Allowances = [new PayItemInput("Transport", 500m)],
        Deductions = [new PayItemInput("Union dues", 50m)],
    };

    public static EmployeeData Data(
        string email = "sara.ahmed@acme.test", EmployeeStatus status = EmployeeStatus.Active) =>
        new("Sara Ahmed", email, "Engineer", "Engineering", new DateOnly(2020, 1, 15), 5000m, status,
            [new PayItem("Transport", 500m)], [new PayItem("Union dues", 50m)]);

    public static Employee Employee(string email = "sara.ahmed@acme.test", EmployeeStatus status = EmployeeStatus.Active) =>
        Domain.Employee.Create(Data(email, status), Today);
}
