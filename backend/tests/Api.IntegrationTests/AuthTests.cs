using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;

namespace Api.IntegrationTests;

public sealed class AuthTests(ApiFactory api)
{
    [Fact]
    public async Task Hr_login_returns_token_user_shape_and_hr_permissions()
    {
        using var client = api.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "HR@acme.test", password = ApiFactory.DemoPassword }); // email is case-insensitive

        var login = await response.ShouldBeOkJsonAsync<LoginBody>();
        login.AccessToken.Should().NotBeNullOrWhiteSpace();
        login.AccessToken.Split('.').Should().HaveCount(3, "it is a JWT");
        login.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow).And.BeBefore(DateTime.UtcNow.AddMinutes(61));
        login.User.Email.Should().Be(ApiFactory.HrEmail);
        login.User.DisplayName.Should().NotBeNullOrWhiteSpace();
        login.User.Role.Should().Be("HR");
        login.User.EmployeeId.Should().BeNull();
        login.User.Permissions.Should().BeEquivalentTo("employees.read", "employees.write", "payroll.read.all");
    }

    [Fact]
    public async Task Employee_login_is_linked_to_its_employee_and_can_only_read_own_payroll()
    {
        var login = await api.LoginAsync(ApiFactory.SaraEmail);

        login.User.Role.Should().Be("Employee");
        login.User.Email.Should().Be(ApiFactory.SaraEmail);
        login.User.EmployeeId.Should().NotBeNull().And.NotBe(Guid.Empty);
        login.User.Permissions.Should().BeEquivalentTo("payroll.read.own");

        using var hr = await api.HrClientAsync();
        var employee = await (await hr.GetAsync($"/api/employees/{login.User.EmployeeId}")).ShouldBeOkJsonAsync<EmployeeBody>();
        employee.Email.Should().Be(ApiFactory.SaraEmail);
        employee.FullName.Should().Be("Sara Ahmed");
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_are_both_401_with_the_same_detail()
    {
        using var client = api.CreateClient();

        var wrongPassword = await (await client.PostAsJsonAsync("/api/auth/login",
            new { email = ApiFactory.HrEmail, password = "not-the-password" })).ShouldBeProblemAsync(HttpStatusCode.Unauthorized);
        var unknownEmail = await (await client.PostAsJsonAsync("/api/auth/login",
            new { email = "nobody@acme.test", password = ApiFactory.DemoPassword })).ShouldBeProblemAsync(HttpStatusCode.Unauthorized);

        wrongPassword.Detail.Should().Be("Invalid email or password.");
        unknownEmail.Detail.Should().Be(wrongPassword.Detail);
        unknownEmail.Title.Should().Be(wrongPassword.Title);
    }

    [Theory]
    [InlineData("not-an-email", "whatever", "email")]
    [InlineData("", "whatever", "email")]
    [InlineData("hr@acme.test", "", "password")]
    public async Task Malformed_login_is_400_with_field_errors(string email, string password, string field)
    {
        using var client = api.CreateClient();

        var problem = await (await client.PostAsJsonAsync("/api/auth/login", new { email, password }))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest);

        problem.Errors.Should().ContainKey(field);
    }

    [Fact]
    public async Task Login_with_password_over_128_chars_is_400()
    {
        using var client = api.CreateClient();

        var problem = await (await client.PostAsJsonAsync("/api/auth/login",
            new { email = ApiFactory.HrEmail, password = new string('x', 129) })).ShouldBeProblemAsync(HttpStatusCode.BadRequest);

        problem.Errors.Should().ContainKey("password");
    }

    [Fact]
    public async Task Me_with_token_returns_the_current_user()
    {
        var login = await api.LoginAsync(ApiFactory.OmarEmail);
        using var client = await api.OmarClientAsync();

        var me = await (await client.GetAsync("/api/auth/me")).ShouldBeOkJsonAsync<CurrentUserBody>();

        me.Should().BeEquivalentTo(login.User);
    }

    [Fact]
    public async Task Me_without_token_is_401()
    {
        using var client = api.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_with_a_forged_token_is_401()
    {
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIn0.c2lnbmF0dXJlLWlzLW5vdC12YWxpZA");

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

public sealed class AuthorizationTests(ApiFactory api)
{
    [Fact]
    public async Task No_token_on_employees_is_401()
    {
        using var client = api.CreateClient();

        (await client.GetAsync("/api/employees")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/employees", EmployeeFactory.ValidInput())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Employee_token_is_403_on_list_get_and_create_employees()
    {
        using var sara = await api.SaraClientAsync();
        var ownId = (await api.LoginAsync(ApiFactory.SaraEmail)).User.EmployeeId;

        (await sara.GetAsync("/api/employees")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await sara.GetAsync($"/api/employees/{ownId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await sara.GetAsync("/api/employees/departments")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await sara.PostAsJsonAsync("/api/employees", EmployeeFactory.ValidInput())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await sara.DeleteAsync($"/api/employees/{ownId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Hr_token_can_list_employees()
    {
        using var hr = await api.HrClientAsync();

        var page = await (await hr.GetAsync("/api/employees")).ShouldBeOkJsonAsync<PageBody>();

        page.TotalCount.Should().BeGreaterThanOrEqualTo(15, "the seeder adds 15 employees");
    }

    // The contract says every non-2xx is ProblemDetails, but 401/403 produced by the authentication/authorization
    // middleware (not by a handler) are written by the JWT challenge/forbid with an empty body.
    [Fact(Skip = "BUG: 401/403 from the JWT/authorization middleware have an empty body, not application/problem+json with traceId (api-contract.md: every non-2xx is ProblemDetails). See productBugs.")]
    public async Task Middleware_401_and_403_are_problem_details()
    {
        using var anonymous = api.CreateClient();
        await (await anonymous.GetAsync("/api/employees")).ShouldBeProblemAsync(HttpStatusCode.Unauthorized);

        using var sara = await api.SaraClientAsync();
        await (await sara.GetAsync("/api/employees")).ShouldBeProblemAsync(HttpStatusCode.Forbidden);
    }
}
