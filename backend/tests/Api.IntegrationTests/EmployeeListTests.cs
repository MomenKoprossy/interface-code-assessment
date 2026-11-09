using System.Net;
using AwesomeAssertions;

namespace Api.IntegrationTests;

/// <summary>
/// Other tests create employees concurrently, so every test here scopes its assertions to its own data: a unique
/// department (exact-match filter) or a unique search token.
/// </summary>
public sealed class EmployeeListTests(ApiFactory api)
{
    private static string Q(string value) => Uri.EscapeDataString(value);

    private async Task<(HttpClient Hr, string Department)> SeedDepartmentAsync(params EmployeeInputBody[] inputs)
    {
        var hr = await api.HrClientAsync();
        var department = "Dept" + EmployeeFactory.Token();
        foreach (var input in inputs)
            await EmployeeFactory.CreateAsync(hr, input with { Department = department });
        return (hr, department);
    }

    private static EmployeeInputBody Named(string name, string status = "Active") =>
        EmployeeFactory.ValidInput(fullName: name, status: status);

    [Fact]
    public async Task Pagination_metadata_is_consistent_across_pages()
    {
        var (hr, dept) = await SeedDepartmentAsync(Named("A1"), Named("A2"), Named("A3"), Named("A4"), Named("A5"));
        using var _ = hr;

        var first = await (await hr.GetAsync($"/api/employees?department={dept}&page=1&pageSize=2")).ShouldBeOkJsonAsync<PageBody>();
        var last = await (await hr.GetAsync($"/api/employees?department={dept}&page=3&pageSize=2")).ShouldBeOkJsonAsync<PageBody>();
        var beyond = await (await hr.GetAsync($"/api/employees?department={dept}&page=4&pageSize=2")).ShouldBeOkJsonAsync<PageBody>();

        first.Should().BeEquivalentTo(new { Page = 1, PageSize = 2, TotalCount = 5L, TotalPages = 3, HasPrevious = false, HasNext = true });
        first.Items.Select(e => e.FullName).Should().Equal("A1", "A2");
        last.Should().BeEquivalentTo(new { Page = 3, PageSize = 2, TotalCount = 5L, TotalPages = 3, HasPrevious = true, HasNext = false });
        last.Items.Select(e => e.FullName).Should().Equal("A5");
        beyond.Items.Should().BeEmpty();
        beyond.HasNext.Should().BeFalse();
    }

    [Fact]
    public async Task Default_paging_is_page_1_size_10()
    {
        using var hr = await api.HrClientAsync();

        var page = await (await hr.GetAsync("/api/employees")).ShouldBeOkJsonAsync<PageBody>();

        page.Page.Should().Be(1);
        page.PageSize.Should().Be(10);
        page.Items.Should().HaveCount(10, "the seeder alone provides 15 employees");
        page.TotalPages.Should().Be((int)Math.Ceiling(page.TotalCount / 10.0));
    }

    [Theory]
    [InlineData("pageSize=1000", 1, 100)]
    [InlineData("pageSize=101", 1, 100)]
    [InlineData("pageSize=0", 1, 1)]
    [InlineData("pageSize=-5", 1, 1)]
    [InlineData("page=0&pageSize=5", 1, 5)]
    [InlineData("page=-3&pageSize=5", 1, 5)]
    public async Task Paging_parameters_are_clamped(string query, int expectedPage, int expectedPageSize)
    {
        using var hr = await api.HrClientAsync();

        var page = await (await hr.GetAsync($"/api/employees?{query}")).ShouldBeOkJsonAsync<PageBody>();

        page.Page.Should().Be(expectedPage);
        page.PageSize.Should().Be(expectedPageSize);
        page.Items.Should().HaveCountLessThanOrEqualTo(expectedPageSize);
    }

    [Fact]
    public async Task Search_matches_part_of_name_case_insensitively()
    {
        var token = EmployeeFactory.Token();
        var (hr, dept) = await SeedDepartmentAsync(Named($"Zed {token} Smith"), Named("Unrelated Person"));
        using var _ = hr;

        var page = await (await hr.GetAsync($"/api/employees?search={Q(token.ToUpperInvariant()[2..10])}")).ShouldBeOkJsonAsync<PageBody>();

        page.Items.Should().ContainSingle().Which.FullName.Should().Be($"Zed {token} Smith");
        page.Items[0].Department.Should().Be(dept);
    }

    [Fact]
    public async Task Search_matches_part_of_email_case_insensitively()
    {
        using var hr = await api.HrClientAsync();
        var created = await EmployeeFactory.CreateAsync(hr, EmployeeFactory.ValidInput(fullName: "Email Search Target"));
        var localPart = created.Email.Split('@')[0];

        var page = await (await hr.GetAsync($"/api/employees?search={Q(localPart[3..15].ToUpperInvariant())}")).ShouldBeOkJsonAsync<PageBody>();

        page.Items.Should().ContainSingle().Which.Id.Should().Be(created.Id);
    }

    [Theory]
    [InlineData("50%", "50% Off", "50x Off")]
    [InlineData("5_a", "Under 5_a", "Under 5xa")]
    [InlineData("\\", "Back\\slash", "Back slash")]
    public async Task Search_wildcards_are_literal(string search, string matching, string notMatching)
    {
        var (hr, dept) = await SeedDepartmentAsync(Named(matching), Named(notMatching));
        using var _ = hr;

        var page = await (await hr.GetAsync($"/api/employees?department={dept}&search={Q(search)}")).ShouldBeOkJsonAsync<PageBody>();

        page.Items.Select(e => e.FullName).Should().Equal(matching);
    }

    [Fact]
    public async Task Department_filter_is_exact_and_case_insensitive()
    {
        var (hr, dept) = await SeedDepartmentAsync(Named("Dept Member One"), Named("Dept Member Two"));
        using var _ = hr;

        var upper = await (await hr.GetAsync($"/api/employees?department={dept.ToUpperInvariant()}")).ShouldBeOkJsonAsync<PageBody>();
        var prefix = await (await hr.GetAsync($"/api/employees?department={dept[..^2]}")).ShouldBeOkJsonAsync<PageBody>();

        upper.TotalCount.Should().Be(2);
        upper.Items.Should().OnlyContain(e => e.Department == dept);
        prefix.TotalCount.Should().Be(0, "department is an exact match, not a substring");
    }

    [Fact]
    public async Task Status_filter_returns_only_that_status()
    {
        var (hr, dept) = await SeedDepartmentAsync(Named("Active One"), Named("Inactive One", "Inactive"), Named("Active Two"));
        using var _ = hr;

        var active = await (await hr.GetAsync($"/api/employees?department={dept}&status=Active")).ShouldBeOkJsonAsync<PageBody>();
        var inactive = await (await hr.GetAsync($"/api/employees?department={dept}&status=Inactive")).ShouldBeOkJsonAsync<PageBody>();

        active.Items.Select(e => e.FullName).Should().Equal("Active One", "Active Two");
        inactive.Items.Select(e => e.FullName).Should().Equal("Inactive One");
    }

    [Fact]
    public async Task Results_are_ordered_by_full_name_then_id()
    {
        var (hr, dept) = await SeedDepartmentAsync(Named("Charlie"), Named("Alpha"), Named("Bravo"), Named("Alpha"));
        using var _ = hr;

        var page = await (await hr.GetAsync($"/api/employees?department={dept}")).ShouldBeOkJsonAsync<PageBody>();

        page.Items.Select(e => e.FullName).Should().Equal("Alpha", "Alpha", "Bravo", "Charlie");
        page.Items.Take(2).Select(e => e.Id.ToString()).Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("active")]
    [InlineData("Retired")]
    public async Task Invalid_status_is_400_with_status_error(string status)
    {
        using var hr = await api.HrClientAsync();

        var problem = await (await hr.GetAsync($"/api/employees?status={status}")).ShouldBeProblemAsync(HttpStatusCode.BadRequest);

        problem.Errors.Should().ContainKey("status");
    }

    [Fact]
    public async Task Departments_are_distinct_and_sorted()
    {
        var (hr, dept) = await SeedDepartmentAsync(Named("Dept Lister One"), Named("Dept Lister Two"));
        using var _ = hr;

        var departments = await (await hr.GetAsync("/api/employees/departments")).ShouldBeOkJsonAsync<string[]>();

        departments.Should().OnlyHaveUniqueItems();
        departments.Should().BeInAscendingOrder(StringComparer.Ordinal);
        departments.Should().Contain(["Engineering", "Finance", "Human Resources", "Operations", "Sales", dept]);
        departments.Count(d => d == dept).Should().Be(1);
    }
}
