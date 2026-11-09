using AwesomeAssertions;
using BuildingBlocks.Abstractions;
using Employees.Application;
using Employees.Domain;
using NSubstitute;

namespace Employees.UnitTests;

public sealed class ListEmployeesTests
{
    private readonly IEmployeeRepository _employees = Substitute.For<IEmployeeRepository>();

    public ListEmployeesTests() =>
        _employees.SearchAsync(default!, default, default)
            .ReturnsForAnyArgs(call => new PagedList<Employee>([TestData.Employee()], call.Arg<PageRequest>().Page, call.Arg<PageRequest>().PageSize, 1));

    [Theory]
    [InlineData(null, null, 1, 10)]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-5, -5, 1, 1)]
    [InlineData(3, 100, 3, 100)]
    [InlineData(2, 101, 2, 100)]
    public async Task Paging_is_defaulted_and_clamped(int? page, int? pageSize, int expectedPage, int expectedSize)
    {
        await Handle(new ListEmployeesQuery(page, pageSize, null, null, null));

        await _employees.Received(1).SearchAsync(Arg.Any<EmployeeSearchCriteria>(),
            Arg.Is<PageRequest>(p => p.Page == expectedPage && p.PageSize == expectedSize), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Filters_are_trimmed_and_status_parsed()
    {
        await Handle(new ListEmployeesQuery(1, 10, "  sara ", " Engineering ", "Inactive"));

        await _employees.Received(1).SearchAsync(new EmployeeSearchCriteria("sara", "Engineering", EmployeeStatus.Inactive),
            Arg.Any<PageRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Blank_filters_mean_no_filter()
    {
        await Handle(new ListEmployeesQuery(1, 10, " ", "", null));

        await _employees.Received(1).SearchAsync(new EmployeeSearchCriteria(null, null, null),
            Arg.Any<PageRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Maps_to_a_paged_list_of_list_items()
    {
        var result = await Handle(new ListEmployeesQuery(1, 10, null, null, null));

        result.Value.Should().BeEquivalentTo(new { Page = 1, PageSize = 10, TotalCount = 1, TotalPages = 1, HasPrevious = false, HasNext = false });
        result.Value.Items.Should().ContainSingle().Which.Email.Should().Be("sara.ahmed@acme.test");
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("Active", true)]
    [InlineData("Inactive", true)]
    [InlineData("Retired", false)]
    public void Status_filter_validation(string? status, bool valid) =>
        new ListEmployeesValidator().Validate(new ListEmployeesQuery(null, null, null, null, status)).IsValid.Should().Be(valid);

    private async Task<Result<PagedList<EmployeeListItem>>> Handle(ListEmployeesQuery query) =>
        await new ListEmployeesHandler(_employees).Handle(query, TestContext.Current.CancellationToken);
}
