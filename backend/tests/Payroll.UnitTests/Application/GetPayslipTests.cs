using AwesomeAssertions;
using BuildingBlocks.Abstractions;
using Employees.Contracts;
using NSubstitute;
using Payroll.Application;
using static Payroll.UnitTests.TestData;

namespace Payroll.UnitTests.Application;

public class GetPayslipTests
{
    private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();
    private readonly IEmployeePayrollProfileProvider _employees = Substitute.For<IEmployeePayrollProfileProvider>();
    private readonly IPayrollPolicyProvider _policies = Substitute.For<IPayrollPolicyProvider>();

    public GetPayslipTests()
    {
        _policies.Current.Returns(DefaultPolicy);
        ActAsHr();
    }

    [Fact]
    public async Task Returns_the_payslip()
    {
        _employees.GetAsync(EmployeeId, Arg.Any<CancellationToken>()).Returns(Employee(5000m));

        var result = await Handle("2026-03");

        result.IsSuccess.Should().BeTrue();
        result.Value.EmployeeId.Should().Be(EmployeeId);
        result.Value.Month.Should().Be("2026-03");
        result.Value.NetPay.Should().Be(4155.00m);
    }

    [Fact]
    public async Task Unknown_employee_is_not_found()
    {
        _employees.GetAsync(EmployeeId, Arg.Any<CancellationToken>()).Returns((EmployeePayrollProfile?)null);

        var result = await Handle("2026-03");

        result.Error!.Value.Type.Should().Be(ErrorType.NotFound);
        result.Error.Value.Message.Should().Be("Employee was not found.");
    }

    [Fact]
    public async Task Another_employees_payslip_is_forbidden_without_revealing_whether_it_exists()
    {
        ActAsEmployee(Guid.NewGuid());

        var result = await Handle("2026-03");

        result.Error!.Value.Type.Should().Be(ErrorType.Forbidden);
        await _employees.DidNotReceiveWithAnyArgs().GetAsync(default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Employee_reads_own_payslip()
    {
        ActAsEmployee(EmployeeId);
        _employees.GetAsync(EmployeeId, Arg.Any<CancellationToken>()).Returns(Employee());

        (await Handle("2026-03")).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Inactive_employee_is_unprocessable()
    {
        _employees.GetAsync(EmployeeId, Arg.Any<CancellationToken>()).Returns(Employee(isActive: false));

        var result = await Handle("2026-03");

        result.Error!.Value.Type.Should().Be(ErrorType.Unprocessable);
        result.Error.Value.Message.Should().Be("Payslip is not available for an inactive employee.");
    }

    [Fact]
    public async Task Month_before_hire_is_unprocessable()
    {
        _employees.GetAsync(EmployeeId, Arg.Any<CancellationToken>()).Returns(Employee(hireDate: new DateOnly(2026, 4, 1)));

        var result = await Handle("2026-03");

        result.Error!.Value.Type.Should().Be(ErrorType.Unprocessable);
        result.Error.Value.Message.Should().Be("Employee was not yet hired in 2026-03.");
    }

    [Fact]
    public async Task Negative_net_is_unprocessable()
    {
        _employees.GetAsync(EmployeeId, Arg.Any<CancellationToken>())
            .Returns(Employee(1000m, deductions: [new PayComponent("Loan", 5000m)]));

        var result = await Handle("2026-03");

        result.Error!.Value.Type.Should().Be(ErrorType.Unprocessable);
        result.Error.Value.Message.Should().Be("Deductions exceed gross pay for 2026-03.");
    }

    [Fact]
    public async Task Malformed_month_reaching_the_handler_is_a_validation_error_on_month()
    {
        var result = await Handle("2026-13");

        result.Error!.Value.Type.Should().Be(ErrorType.Validation);
        result.Error.Value.Field.Should().Be("month");
    }

    [Theory]
    [InlineData(null, "Month is required.")]
    [InlineData("", "Month is required.")]
    [InlineData("2026-3", PayrollMonthFormat)]
    [InlineData("2026-13", PayrollMonthFormat)]
    [InlineData("26-03", PayrollMonthFormat)]
    public void Validator_rejects_missing_or_malformed_month(string? month, string message)
    {
        var result = new GetPayslipValidator().Validate(new GetPayslipQuery(EmployeeId, month));

        result.Errors.Should().ContainSingle()
            .Which.Should().Match<FluentValidation.Results.ValidationFailure>(f => f.PropertyName == "Month" && f.ErrorMessage == message);
    }

    [Fact]
    public void Validator_accepts_a_well_formed_month() =>
        new GetPayslipValidator().Validate(new GetPayslipQuery(EmployeeId, "2026-03")).IsValid.Should().BeTrue();

    private const string PayrollMonthFormat = Payroll.Domain.PayrollMonth.FormatMessage;

    private ValueTask<Result<Payroll.Domain.Payslip>> Handle(string? month) =>
        new GetPayslipHandler(new PayslipAccessPolicy(_user), _employees, _policies, Calculator())
            .Handle(new GetPayslipQuery(EmployeeId, month), CancellationToken.None);

    private void ActAsHr() => _user.HasPermission("payroll.read.all").Returns(true);

    private void ActAsEmployee(Guid ownEmployeeId)
    {
        _user.HasPermission("payroll.read.all").Returns(false);
        _user.HasPermission("payroll.read.own").Returns(true);
        _user.FindClaim("employee_id").Returns(ownEmployeeId.ToString());
    }
}
