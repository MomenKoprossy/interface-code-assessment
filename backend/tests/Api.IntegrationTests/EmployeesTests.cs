using System.Net;
using System.Net.Http.Json;
using System.Text;
using AwesomeAssertions;
using EmployeePayroll.Api.Persistence;
using Employees.Domain;
using Employees.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.IntegrationTests;

public sealed class EmployeesCrudTests(ApiFactory api)
{
    [Fact]
    public async Task Create_returns_201_with_location_that_gets_the_same_resource()
    {
        using var hr = await api.HrClientAsync();
        var input = EmployeeFactory.ValidInput(
            fullName: "  Grace Hopper  ",
            allowances: [new("Housing", 1500.50m), new("Meal", 300m)],
            deductions: [new("Union dues", 50.25m)]) with { Email = $"  Grace.{Guid.NewGuid():N}@ACME.test " };

        var response = await hr.PostAsJsonAsync("/api/employees", input);

        var created = await response.ShouldBeOkJsonAsync<EmployeeBody>(HttpStatusCode.Created);
        created.Id.Should().NotBe(Guid.Empty);
        created.FullName.Should().Be("Grace Hopper", "text fields are trimmed");
        created.Email.Should().Be(input.Email!.Trim().ToLowerInvariant(), "email is trimmed and lower-cased");
        created.JobTitle.Should().Be(input.JobTitle);
        created.Department.Should().Be(input.Department);
        created.HireDate.Should().Be(input.HireDate!.Value);
        created.BaseMonthlySalary.Should().Be(input.BaseMonthlySalary!.Value);
        created.Status.Should().Be("Active");
        created.Allowances.Should().Equal(new PayItemBody("Housing", 1500.50m), new PayItemBody("Meal", 300m));
        created.Deductions.Should().Equal(new PayItemBody("Union dues", 50.25m));

        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.OriginalString.Should().EndWith($"/api/employees/{created.Id}");
        var fetched = await (await hr.GetAsync(response.Headers.Location)).ShouldBeOkJsonAsync<EmployeeBody>();
        fetched.Should().BeEquivalentTo(created);
    }

    [Fact]
    public async Task Get_unknown_id_is_404_problem()
    {
        using var hr = await api.HrClientAsync();

        var problem = await (await hr.GetAsync($"/api/employees/{Guid.NewGuid()}")).ShouldBeProblemAsync(HttpStatusCode.NotFound);

        problem.Detail.Should().Be("Employee was not found.");
    }

    [Fact]
    public async Task Put_replaces_fields_and_pay_items()
    {
        using var hr = await api.HrClientAsync();
        var created = await EmployeeFactory.CreateAsync(hr, EmployeeFactory.ValidInput(
            allowances: [new("Transport", 500m), new("Housing", 1500m)], deductions: [new("Loan", 400m)]));
        var update = EmployeeFactory.ValidInput(
            fullName: "Renamed Person", department: "Finance", salary: 6100.75m, status: "Inactive",
            allowances: [new("Meal", 250m)], deductions: []);

        var updated = await (await hr.PutAsJsonAsync($"/api/employees/{created.Id}", update)).ShouldBeOkJsonAsync<EmployeeBody>();

        updated.Id.Should().Be(created.Id);
        updated.FullName.Should().Be("Renamed Person");
        updated.Email.Should().Be(update.Email);
        updated.Department.Should().Be("Finance");
        updated.BaseMonthlySalary.Should().Be(6100.75m);
        updated.Status.Should().Be("Inactive");
        updated.Allowances.Should().Equal(new PayItemBody("Meal", 250m));
        updated.Deductions.Should().BeEmpty();

        var fetched = await (await hr.GetAsync($"/api/employees/{created.Id}")).ShouldBeOkJsonAsync<EmployeeBody>();
        fetched.Should().BeEquivalentTo(updated, "the replacement was persisted, not merged");
    }

    [Fact]
    public async Task Put_unknown_id_is_404()
    {
        using var hr = await api.HrClientAsync();

        await (await hr.PutAsJsonAsync($"/api/employees/{Guid.NewGuid()}", EmployeeFactory.ValidInput()))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_is_204_then_get_is_404_and_second_delete_is_404()
    {
        using var hr = await api.HrClientAsync();
        var created = await EmployeeFactory.CreateAsync(hr, EmployeeFactory.ValidInput());

        var delete = await hr.DeleteAsync($"/api/employees/{created.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await (await hr.GetAsync($"/api/employees/{created.Id}")).ShouldBeProblemAsync(HttpStatusCode.NotFound);
        await (await hr.DeleteAsync($"/api/employees/{created.Id}")).ShouldBeProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deleted_employee_disappears_from_the_list()
    {
        using var hr = await api.HrClientAsync();
        var created = await EmployeeFactory.CreateAsync(hr, EmployeeFactory.ValidInput());

        (await hr.DeleteAsync($"/api/employees/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var page = await (await hr.GetAsync($"/api/employees?search={created.Email}")).ShouldBeOkJsonAsync<PageBody>();
        page.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Binding_failures_are_400_problem_details()
    {
        using var hr = await api.HrClientAsync();

        await (await hr.PostAsync("/api/employees", new StringContent("{ this is not json", Encoding.UTF8, "application/json")))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest);
        await (await hr.PostAsync("/api/employees", new StringContent("""{"baseMonthlySalary":"abc"}""", Encoding.UTF8, "application/json")))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest);
        await (await hr.GetAsync("/api/employees?page=abc")).ShouldBeProblemAsync(HttpStatusCode.BadRequest);
    }
}

public sealed class EmployeeValidationTests(ApiFactory api)
{
    public static TheoryData<string, EmployeeInputBody, string> InvalidInputs()
    {
        var valid = EmployeeFactory.ValidInput();
        return new()
        {
            { "fullName 101 chars", valid with { FullName = new string('a', 101) }, "fullName" },
            { "fullName blank", valid with { FullName = "   " }, "fullName" },
            { "email without domain dot", valid with { Email = "someone@acme" }, "email" },
            { "email with a space", valid with { Email = "some one@acme.test" }, "email" },
            { "hireDate tomorrow (UTC)", valid with { HireDate = ApiFactory.Today.AddDays(1) }, "hireDate" },
            { "hireDate before 1900", valid with { HireDate = new DateOnly(1899, 12, 31) }, "hireDate" },
            { "salary 0", valid with { BaseMonthlySalary = 0m }, "baseMonthlySalary" },
            { "salary 3 decimals", valid with { BaseMonthlySalary = 1000.123m }, "baseMonthlySalary" },
            { "salary above 1,000,000", valid with { BaseMonthlySalary = 1_000_000.01m }, "baseMonthlySalary" },
            { "allowance amount 0", valid with { Allowances = [new("Transport", 0m)] }, "allowances[0].amount" },
            { "allowance name 51 chars", valid with { Allowances = [new(new string('n', 51), 10m)] }, "allowances[0].name" },
            { "duplicate allowance names (case-insensitive)", valid with { Allowances = [new("Meal", 10m), new("meal", 20m)] }, "allowances[1].name" },
            { "deduction amount 3 decimals", valid with { Deductions = [new("Loan", 10.001m)] }, "deductions[0].amount" },
            { "invalid status", valid with { Status = "Retired" }, "status" },
            { "status wrong case", valid with { Status = "active" }, "status" },
        };
    }

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public async Task Create_with_invalid_field_is_400_problem_with_camelCase_key(string because, EmployeeInputBody input, string field)
    {
        using var hr = await api.HrClientAsync();

        var problem = await (await hr.PostAsJsonAsync("/api/employees", input)).ShouldBeProblemAsync(HttpStatusCode.BadRequest);

        problem.Title.Should().Be("Validation failed", because);
        problem.Type.Should().Be("https://tools.ietf.org/html/rfc9110#section-15.5.1");
        problem.Errors.Keys.Should().Equal([field], because);
        problem.Errors[field].Should().ContainSingle().Which.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Boundary_values_are_accepted()
    {
        using var hr = await api.HrClientAsync();
        var input = EmployeeFactory.ValidInput(fullName: new string('a', 100), salary: 0.01m, hireDate: ApiFactory.Today,
            allowances: [new(new string('n', 50), 1_000_000m)]);

        var created = await EmployeeFactory.CreateAsync(hr, input);

        created.FullName.Should().HaveLength(100);
        created.BaseMonthlySalary.Should().Be(0.01m);
        created.HireDate.Should().Be(ApiFactory.Today);
    }

    [Fact]
    public async Task Update_runs_the_same_validation()
    {
        using var hr = await api.HrClientAsync();
        var created = await EmployeeFactory.CreateAsync(hr, EmployeeFactory.ValidInput());

        var problem = await (await hr.PutAsJsonAsync($"/api/employees/{created.Id}",
            EmployeeFactory.ValidInput() with { BaseMonthlySalary = 0m })).ShouldBeProblemAsync(HttpStatusCode.BadRequest);

        problem.Errors.Should().ContainKey("baseMonthlySalary");
    }

    [Fact]
    public async Task Missing_required_fields_are_all_reported()
    {
        using var hr = await api.HrClientAsync();

        var problem = await (await hr.PostAsJsonAsync("/api/employees", new { })).ShouldBeProblemAsync(HttpStatusCode.BadRequest);

        problem.Errors.Keys.Should().Contain(["fullName", "email", "jobTitle", "department", "hireDate", "baseMonthlySalary", "status"]);
    }
}

public sealed class EmailUniquenessTests(ApiFactory api)
{
    [Fact]
    public async Task Duplicate_email_is_409_with_email_error()
    {
        using var hr = await api.HrClientAsync();
        var first = await EmployeeFactory.CreateAsync(hr, EmployeeFactory.ValidInput());

        var problem = await (await hr.PostAsJsonAsync("/api/employees", EmployeeFactory.ValidInput() with { Email = first.Email }))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict);

        problem.Errors.Should().ContainKey("email").WhoseValue.Should().Equal("Email is already used by another employee.");
    }

    [Fact]
    public async Task Duplicate_email_differing_only_in_case_is_409()
    {
        using var hr = await api.HrClientAsync();
        var first = await EmployeeFactory.CreateAsync(hr, EmployeeFactory.ValidInput());

        var problem = await (await hr.PostAsJsonAsync("/api/employees",
            EmployeeFactory.ValidInput() with { Email = first.Email.ToUpperInvariant() })).ShouldBeProblemAsync(HttpStatusCode.Conflict);

        problem.Errors.Should().ContainKey("email");
    }

    [Fact]
    public async Task Update_to_another_employees_email_is_409()
    {
        using var hr = await api.HrClientAsync();
        var first = await EmployeeFactory.CreateAsync(hr, EmployeeFactory.ValidInput());
        var second = await EmployeeFactory.CreateAsync(hr, EmployeeFactory.ValidInput());

        var problem = await (await hr.PutAsJsonAsync($"/api/employees/{second.Id}",
            EmployeeFactory.ValidInput() with { Email = first.Email })).ShouldBeProblemAsync(HttpStatusCode.Conflict);

        problem.Errors.Should().ContainKey("email");
    }

    [Fact]
    public async Task Update_keeping_own_email_is_200()
    {
        using var hr = await api.HrClientAsync();
        var created = await EmployeeFactory.CreateAsync(hr, EmployeeFactory.ValidInput());

        var updated = await (await hr.PutAsJsonAsync($"/api/employees/{created.Id}",
            EmployeeFactory.ValidInput(fullName: "Same Email Different Name") with { Email = created.Email.ToUpperInvariant() }))
            .ShouldBeOkJsonAsync<EmployeeBody>();

        updated.Email.Should().Be(created.Email);
        updated.FullName.Should().Be("Same Email Different Name");
    }

    [Fact]
    public async Task Email_of_a_deleted_employee_can_be_reused()
    {
        using var hr = await api.HrClientAsync();
        var deleted = await EmployeeFactory.CreateAsync(hr, EmployeeFactory.ValidInput());
        (await hr.DeleteAsync($"/api/employees/{deleted.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var reused = await EmployeeFactory.CreateAsync(hr, EmployeeFactory.ValidInput() with { Email = deleted.Email });

        reused.Id.Should().NotBe(deleted.Id);
        reused.Email.Should().Be(deleted.Email);
    }

    // Every API-level duplicate hits the handler's pre-check first, so this bypasses it to pin the race backstop:
    // the real constraint name Postgres reports and the way Npgsql wraps the violation.
    [Fact]
    public async Task Unique_index_violation_maps_to_409_with_email_error()
    {
        using var hr = await api.HrClientAsync();
        var first = await EmployeeFactory.CreateAsync(hr, EmployeeFactory.ValidInput());
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Add(Employee.Create(new EmployeeData(
            "Racing Duplicate", first.Email, "Engineer", "Engineering", new DateOnly(2020, 1, 1), 1000m,
            EmployeeStatus.Active, [], [])));

        var thrown = await FluentActions.Awaiting(() => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();

        var mapper = new UniqueEmailViolationMapper();
        var problem = mapper.TryMap(thrown.Which);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Extensions["errors"].Should().BeOfType<Dictionary<string, string[]>>().Which.Should().ContainKey("email");
        mapper.TryMap(new DbUpdateException("x", new InvalidOperationException())).Should().BeNull();
    }
}
