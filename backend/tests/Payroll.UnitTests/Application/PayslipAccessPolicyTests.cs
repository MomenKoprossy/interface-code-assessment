using AwesomeAssertions;
using BuildingBlocks.Abstractions;
using NSubstitute;
using Payroll.Application;

namespace Payroll.UnitTests.Application;

public class PayslipAccessPolicyTests
{
    private static readonly Guid Own = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Other = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static TheoryData<string, string[], string?, Guid, bool> Matrix => new()
    {
        { "HR reads any payslip", ["payroll.read.all"], null, Other, true },
        { "HR with an employee_id still reads any", ["payroll.read.all"], Own.ToString(), Other, true },
        { "Employee reads own", ["payroll.read.own"], Own.ToString(), Own, true },
        { "Employee reads own (upper-case id claim)", ["payroll.read.own"], Own.ToString().ToUpperInvariant(), Own, true },
        { "Employee reads another's", ["payroll.read.own"], Own.ToString(), Other, false },
        { "Employee without employee_id claim", ["payroll.read.own"], null, Own, false },
        { "Employee with malformed employee_id claim", ["payroll.read.own"], "not-a-guid", Own, false },
        { "Matching employee_id but no permission", [], Own.ToString(), Own, false },
        { "Unrelated permission only", ["employees.read"], Own.ToString(), Own, false },
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Allows_read_all_or_own_only(string scenario, string[] permissions, string? employeeIdClaim, Guid requested, bool allowed)
    {
        var user = Substitute.For<ICurrentUser>();
        user.HasPermission(Arg.Any<string>()).Returns(call => permissions.Contains(call.Arg<string>()));
        user.FindClaim("employee_id").Returns(employeeIdClaim);

        var result = new PayslipAccessPolicy(user).EnsureCanRead(requested);

        result.IsSuccess.Should().Be(allowed, scenario);
        if (!allowed)
            result.Error!.Value.Type.Should().Be(ErrorType.Forbidden);
    }
}
