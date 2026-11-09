using System.Net;
using AwesomeAssertions;

namespace Api.IntegrationTests;

/// <summary>
/// Payslips through HTTP with the appsettings.json policy: USD, SI 11% of base capped at 1,100.00 and deducted before
/// tax, brackets 0% ≤1,500 · 10% ≤5,000 · 20% ≤10,000 · 25% above. Expected numbers are computed by hand below.
/// </summary>
public sealed class PayrollTests(ApiFactory api)
{
    private static string PayslipUrl(Guid id, string month) => $"/api/employees/{id}/payslip?month={month}";

    [Fact]
    public async Task Hr_gets_a_payslip_that_matches_the_hand_computation()
    {
        using var hr = await api.HrClientAsync();
        var employee = await EmployeeFactory.CreateAsync(hr, EmployeeFactory.ValidInput(
            fullName: "Payroll Hand Check", department: "Payroll QA", salary: 7000.05m, hireDate: new DateOnly(2022, 3, 10),
            allowances: [new("Transport", 500.50m), new("Meal", 299.45m)],
            deductions: [new("Loan", 250.25m)]));

        var payslip = await (await hr.GetAsync(PayslipUrl(employee.Id, "2025-07"))).ShouldBeOkJsonAsync<PayslipBody>();

        // gross        = 7000.05 + 500.50 + 299.45                    = 7800.00
        // SI           = round(7000.05 × 0.11 = 770.0055)             =  770.01  (half away from zero; < cap 1100)
        // taxable      = 7800.00 − 770.01                             = 7029.99
        // tax          = 1500 × 0%  = 0.00
        //              + 3500 × 10% = 350.00
        //              + 2029.99 × 20% = 405.998 → 406.00             =  756.00
        // other        = 250.25
        // deductions   = 770.01 + 756.00 + 250.25                     = 1776.26
        // net          = 7800.00 − 1776.26                            = 6023.74
        payslip.EmployeeId.Should().Be(employee.Id);
        payslip.EmployeeName.Should().Be("Payroll Hand Check");
        payslip.Department.Should().Be("Payroll QA");
        payslip.JobTitle.Should().Be(employee.JobTitle);
        payslip.Month.Should().Be("2025-07");
        payslip.Currency.Should().Be("USD");
        payslip.BaseSalary.Should().Be(7000.05m);
        payslip.Allowances.Should().Equal(new PayItemBody("Transport", 500.50m), new PayItemBody("Meal", 299.45m));
        payslip.TotalAllowances.Should().Be(799.95m);
        payslip.GrossPay.Should().Be(7800.00m);
        payslip.SocialInsurance.Should().Be(new SocialInsuranceBody(0.11m, 1100.00m, 770.01m, 770.01m, false));
        payslip.TaxableIncome.Should().Be(7029.99m);
        payslip.IncomeTax.Amount.Should().Be(756.00m);
        payslip.IncomeTax.Brackets.Should().Equal(
            new TaxBracketLineBody(0m, 1500m, 0.00m, 1500.00m, 0.00m),
            new TaxBracketLineBody(1500m, 5000m, 0.10m, 3500.00m, 350.00m),
            new TaxBracketLineBody(5000m, 10000m, 0.20m, 2029.99m, 406.00m));
        payslip.OtherDeductions.Should().Equal(new PayItemBody("Loan", 250.25m));
        payslip.TotalOtherDeductions.Should().Be(250.25m);
        payslip.TotalDeductions.Should().Be(1776.26m);
        payslip.NetPay.Should().Be(6023.74m);
        payslip.RoundingRule.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Social_insurance_cap_is_applied_for_a_high_salary()
    {
        using var hr = await api.HrClientAsync();
        var employee = await EmployeeFactory.CreateAsync(hr, EmployeeFactory.ValidInput(salary: 20000m, allowances: []));

        var payslip = await (await hr.GetAsync(PayslipUrl(employee.Id, "2025-07"))).ShouldBeOkJsonAsync<PayslipBody>();

        // SI uncapped 20000 × 0.11 = 2200.00 → capped 1100.00; taxable 18900.00
        // tax = 0 + 350.00 + 1000.00 + (8900 × 25% = 2225.00) = 3575.00; net = 20000 − 1100 − 3575 = 15325.00
        payslip.SocialInsurance.Should().Be(new SocialInsuranceBody(0.11m, 1100.00m, 2200.00m, 1100.00m, true));
        payslip.TaxableIncome.Should().Be(18900.00m);
        payslip.IncomeTax.Amount.Should().Be(3575.00m);
        payslip.IncomeTax.Brackets.Should().HaveCount(4);
        payslip.IncomeTax.Brackets[^1].Should().Be(new TaxBracketLineBody(10000m, null, 0.25m, 8900.00m, 2225.00m));
        payslip.Allowances.Should().BeEmpty();
        payslip.TotalAllowances.Should().Be(0m);
        payslip.TotalDeductions.Should().Be(4675.00m);
        payslip.NetPay.Should().Be(15325.00m);
    }

    [Fact]
    public async Task Taxable_income_exactly_on_a_bracket_boundary_is_taxed_at_the_lower_rate()
    {
        using var hr = await api.HrClientAsync();
        // base 1000 → SI 110.00; allowance 4110 → gross 5110, taxable exactly 5000.00
        var employee = await EmployeeFactory.CreateAsync(hr, EmployeeFactory.ValidInput(
            salary: 1000m, allowances: [new("Top-up", 4110m)]));

        var payslip = await (await hr.GetAsync(PayslipUrl(employee.Id, "2025-07"))).ShouldBeOkJsonAsync<PayslipBody>();

        payslip.TaxableIncome.Should().Be(5000.00m);
        payslip.IncomeTax.Brackets.Should().HaveCount(2, "the 20% bracket starts above 5000");
        payslip.IncomeTax.Amount.Should().Be(350.00m);
        payslip.NetPay.Should().Be(5110m - 110m - 350m);
    }

    [Fact]
    public async Task Employee_gets_own_payslip()
    {
        var sara = await api.LoginAsync(ApiFactory.SaraEmail);
        using var client = await api.SaraClientAsync();

        var payslip = await (await client.GetAsync(PayslipUrl(sara.User.EmployeeId!.Value, "2025-07"))).ShouldBeOkJsonAsync<PayslipBody>();

        // Seeded: base 8500, Transport 500 + Housing 1500, no deductions.
        // SI 935.00; taxable 9565.00; tax 0 + 350.00 + (4565 × 20% = 913.00) = 1263.00; net 10500 − 935 − 1263 = 8302.00
        payslip.EmployeeId.Should().Be(sara.User.EmployeeId.Value);
        payslip.EmployeeName.Should().Be("Sara Ahmed");
        payslip.GrossPay.Should().Be(10500m);
        payslip.SocialInsurance.Amount.Should().Be(935.00m);
        payslip.TaxableIncome.Should().Be(9565.00m);
        payslip.IncomeTax.Amount.Should().Be(1263.00m);
        payslip.NetPay.Should().Be(8302.00m);
    }

    [Fact]
    public async Task Employee_asking_for_another_employees_payslip_is_403()
    {
        var omar = await api.LoginAsync(ApiFactory.OmarEmail);
        using var sara = await api.SaraClientAsync();

        var problem = await (await sara.GetAsync(PayslipUrl(omar.User.EmployeeId!.Value, "2025-07"))).ShouldBeProblemAsync(HttpStatusCode.Forbidden);

        problem.Detail.Should().Be("You can only view your own payslip.");
    }

    [Fact]
    public async Task Employee_asking_for_a_random_id_is_403_not_404()
    {
        using var sara = await api.SaraClientAsync();

        await (await sara.GetAsync(PayslipUrl(Guid.NewGuid(), "2025-07"))).ShouldBeProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Hr_asking_for_an_unknown_employee_is_404()
    {
        using var hr = await api.HrClientAsync();

        await (await hr.GetAsync(PayslipUrl(Guid.NewGuid(), "2025-07"))).ShouldBeProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Payslip_of_a_deleted_employee_is_404()
    {
        using var hr = await api.HrClientAsync();
        var employee = await EmployeeFactory.CreateAsync(hr, EmployeeFactory.ValidInput());
        (await hr.DeleteAsync($"/api/employees/{employee.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await (await hr.GetAsync(PayslipUrl(employee.Id, "2025-07"))).ShouldBeProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Inactive_employee_is_422()
    {
        using var hr = await api.HrClientAsync();
        var employee = await EmployeeFactory.CreateAsync(hr, EmployeeFactory.ValidInput(status: "Inactive"));

        var problem = await (await hr.GetAsync(PayslipUrl(employee.Id, "2025-07"))).ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity);

        problem.Detail.Should().Be("Payslip is not available for an inactive employee.");
    }

    [Fact]
    public async Task Month_before_the_hire_month_is_422_and_the_hire_month_itself_is_paid()
    {
        using var hr = await api.HrClientAsync();
        var employee = await EmployeeFactory.CreateAsync(hr, EmployeeFactory.ValidInput(hireDate: new DateOnly(2024, 5, 31)));

        var before = await (await hr.GetAsync(PayslipUrl(employee.Id, "2024-04"))).ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity);
        var hireMonth = await hr.GetAsync(PayslipUrl(employee.Id, "2024-05"));

        before.Detail.Should().Be("Employee was not yet hired in 2024-04.");
        (await hireMonth.ShouldBeOkJsonAsync<PayslipBody>()).GrossPay.Should().Be(5500m, "hired mid-month still gets the full month");
    }

    [Fact]
    public async Task Deductions_exceeding_gross_is_422()
    {
        using var hr = await api.HrClientAsync();
        // gross 1000; SI 110.00; tax 0; other 1000 → total deductions 1110 > gross
        var employee = await EmployeeFactory.CreateAsync(hr, EmployeeFactory.ValidInput(
            salary: 1000m, allowances: [], deductions: [new("Loan", 1000m)]));

        var problem = await (await hr.GetAsync(PayslipUrl(employee.Id, "2025-07"))).ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity);

        problem.Detail.Should().Be("Deductions exceed gross pay for 2025-07.");
    }

    [Fact]
    public async Task Net_pay_of_exactly_zero_is_allowed()
    {
        using var hr = await api.HrClientAsync();
        // gross 1000; SI 110.00; tax 0; other 890 → net exactly 0.00
        var employee = await EmployeeFactory.CreateAsync(hr, EmployeeFactory.ValidInput(
            salary: 1000m, allowances: [], deductions: [new("Loan", 890m)]));

        var payslip = await (await hr.GetAsync(PayslipUrl(employee.Id, "2025-07"))).ShouldBeOkJsonAsync<PayslipBody>();

        payslip.NetPay.Should().Be(0m);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?month=")]
    [InlineData("?month=garbage")]
    [InlineData("?month=2025-13")]
    [InlineData("?month=2025-7")]
    [InlineData("?month=1899-12")]
    [InlineData("?month=2025-07-01")]
    public async Task Missing_or_malformed_month_is_400_with_month_error(string query)
    {
        using var hr = await api.HrClientAsync();
        var sara = await api.LoginAsync(ApiFactory.SaraEmail);

        var problem = await (await hr.GetAsync($"/api/employees/{sara.User.EmployeeId}/payslip{query}"))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest);

        problem.Errors.Should().ContainKey("month");
    }

    [Fact]
    public async Task Payslip_without_token_is_401()
    {
        using var client = api.CreateClient();

        (await client.GetAsync(PayslipUrl(Guid.NewGuid(), "2025-07"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Policy_endpoint_returns_the_configured_rules()
    {
        using var sara = await api.SaraClientAsync(); // any authenticated user

        var policy = await (await sara.GetAsync("/api/payroll/policy")).ShouldBeOkJsonAsync<PolicyBody>();

        policy.Currency.Should().Be("USD");
        policy.DeductSocialInsuranceBeforeTax.Should().BeTrue();
        policy.SocialInsurance.Should().Be(new PolicySocialInsuranceBody(0.11m, 1100.00m));
        policy.TaxBrackets.Should().Equal(
            new PolicyBracketBody(1500.00m, 0.00m), new PolicyBracketBody(5000.00m, 0.10m),
            new PolicyBracketBody(10000.00m, 0.20m), new PolicyBracketBody(null, 0.25m));
        policy.RoundingRule.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Policy_endpoint_without_token_is_401()
    {
        using var client = api.CreateClient();

        (await client.GetAsync("/api/payroll/policy")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    public sealed record PolicySocialInsuranceBody(decimal Rate, decimal? MonthlyCap);
    public sealed record PolicyBracketBody(decimal? UpTo, decimal Rate);
    public sealed record PolicyBody(
        string Currency, bool DeductSocialInsuranceBeforeTax, PolicySocialInsuranceBody SocialInsurance,
        PolicyBracketBody[] TaxBrackets, string RoundingRule);
}
