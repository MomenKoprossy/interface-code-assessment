using AwesomeAssertions;
using BuildingBlocks.Abstractions;
using Employees.Application;
using Employees.Contracts;
using Employees.Domain;
using NSubstitute;

namespace Employees.UnitTests;

public sealed class EmployeeHandlerTests
{
    private readonly IEmployeeRepository _employees = Substitute.For<IEmployeeRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Create_adds_saves_and_returns_normalized_details()
    {
        var command = TestData.ValidCreate() with { FullName = " Sara Ahmed ", Email = "Sara.Ahmed@Acme.Test" };

        var result = await new CreateEmployeeHandler(_employees, _unitOfWork).Handle(command, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.FullName.Should().Be("Sara Ahmed");
        result.Value.Email.Should().Be("sara.ahmed@acme.test");
        result.Value.Status.Should().Be(EmployeeStatus.Active);
        result.Value.Allowances.Should().Equal(new PayItemDto("Transport", 500m));
        result.Value.Deductions.Should().Equal(new PayItemDto("Union dues", 50m));
        await _employees.Received(1).AddAsync(Arg.Is<Employee>(e => e.Id == result.Value.Id), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("sara.ahmed@acme.test")]
    [InlineData("SARA.Ahmed@ACME.test")]
    [InlineData("  sara.ahmed@acme.test ")]
    public async Task Create_with_an_email_in_use_is_a_conflict_on_email(string email)
    {
        _employees.FindIdByEmailAsync("sara.ahmed@acme.test", Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());

        var result = await new CreateEmployeeHandler(_employees, _unitOfWork)
            .Handle(TestData.ValidCreate() with { Email = email }, TestContext.Current.CancellationToken);

        result.Error.Should().Be(Error.Conflict("Email is already used by another employee.", "email"));
        await _employees.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Update_applies_changes_and_saves()
    {
        var employee = TestData.Employee();
        _employees.FindAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee);
        var command = Update(employee.Id) with { JobTitle = "Lead", Status = "Inactive", Allowances = [] };

        var result = await new UpdateEmployeeHandler(_employees, _unitOfWork).Handle(command, TestContext.Current.CancellationToken);

        result.Value.JobTitle.Should().Be("Lead");
        result.Value.Status.Should().Be(EmployeeStatus.Inactive);
        result.Value.Allowances.Should().BeEmpty();
        employee.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeDeactivated>();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_may_keep_its_own_email()
    {
        var employee = TestData.Employee();
        _employees.FindAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee);
        _employees.FindIdByEmailAsync("sara.ahmed@acme.test", Arg.Any<CancellationToken>()).Returns(employee.Id);

        var result = await new UpdateEmployeeHandler(_employees, _unitOfWork)
            .Handle(Update(employee.Id) with { Email = "SARA.AHMED@acme.test" }, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Update_to_another_employees_email_is_a_conflict()
    {
        var employee = TestData.Employee();
        _employees.FindAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee);
        _employees.FindIdByEmailAsync("omar.hassan@acme.test", Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());

        var result = await new UpdateEmployeeHandler(_employees, _unitOfWork)
            .Handle(Update(employee.Id) with { Email = "Omar.Hassan@acme.test" }, TestContext.Current.CancellationToken);

        result.Error.Should().Be(EmployeeErrors.EmailInUse);
        employee.Email.Should().Be("sara.ahmed@acme.test");
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Update_of_a_missing_employee_is_not_found()
    {
        var result = await new UpdateEmployeeHandler(_employees, _unitOfWork).Handle(Update(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error?.Type.Should().Be(ErrorType.NotFound);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Delete_removes_and_saves()
    {
        var employee = TestData.Employee();
        _employees.FindAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee);

        var result = await new DeleteEmployeeHandler(_employees, _unitOfWork).Handle(new DeleteEmployeeCommand(employee.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        _employees.Received(1).Remove(employee);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_of_a_missing_employee_is_not_found()
    {
        var result = await new DeleteEmployeeHandler(_employees, _unitOfWork).Handle(new DeleteEmployeeCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error?.Type.Should().Be(ErrorType.NotFound);
        _employees.DidNotReceiveWithAnyArgs().Remove(default!);
    }

    [Fact]
    public async Task Get_by_id_returns_details()
    {
        var employee = TestData.Employee();
        _employees.FindAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee);

        var result = await new GetEmployeeByIdHandler(_employees).Handle(new GetEmployeeByIdQuery(employee.Id), TestContext.Current.CancellationToken);

        result.Value.Should().BeEquivalentTo(new
        {
            employee.Id,
            FullName = "Sara Ahmed",
            Email = "sara.ahmed@acme.test",
            HireDate = new DateOnly(2020, 1, 15),
            BaseMonthlySalary = 5000m,
            Allowances = new[] { new PayItemDto("Transport", 500m) },
        });
    }

    [Fact]
    public async Task Get_by_id_of_a_missing_employee_is_not_found()
    {
        var result = await new GetEmployeeByIdHandler(_employees).Handle(new GetEmployeeByIdQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error?.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task List_departments_passes_through()
    {
        _employees.ListDepartmentsAsync(Arg.Any<CancellationToken>()).Returns(["Engineering", "Finance"]);

        var result = await new ListDepartmentsHandler(_employees).Handle(new ListDepartmentsQuery(), TestContext.Current.CancellationToken);

        result.Value.Should().Equal("Engineering", "Finance");
    }

    private static UpdateEmployeeCommand Update(Guid id)
    {
        var create = TestData.ValidCreate();
        return new UpdateEmployeeCommand
        {
            Id = id,
            FullName = create.FullName,
            Email = create.Email,
            JobTitle = create.JobTitle,
            Department = create.Department,
            HireDate = create.HireDate,
            BaseMonthlySalary = create.BaseMonthlySalary,
            Status = create.Status,
            Allowances = create.Allowances,
            Deductions = create.Deductions,
        };
    }
}
