using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using BuildingBlocks.Application;
using Employees.Contracts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Api.IntegrationTests;

/// <summary>
/// EmployeeDeactivated is the seam other modules subscribe to: AppDbContextBase saves, then the dispatcher hands the
/// event to every registered IDomainEventHandler. This proves that path end to end through the API.
/// </summary>
public sealed class DomainEventTests(ApiFactory api)
{
    private sealed class CapturingHandler : IDomainEventHandler<EmployeeDeactivated>
    {
        public ConcurrentQueue<Guid> Received { get; } = new();

        public Task Handle(EmployeeDeactivated domainEvent, CancellationToken cancellationToken)
        {
            Received.Enqueue(domainEvent.EmployeeId);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task EmployeeDeactivated_reaches_a_handler_only_on_Active_to_Inactive()
    {
        var handler = new CapturingHandler();
        await using var host = api.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddSingleton<IDomainEventHandler<EmployeeDeactivated>>(handler)));
        using var hr = host.CreateClient();
        hr.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", (await api.LoginAsync(ApiFactory.HrEmail)).AccessToken);

        var deactivated = await EmployeeFactory.CreateAsync(hr, EmployeeFactory.ValidInput());
        var renamed = await EmployeeFactory.CreateAsync(hr, EmployeeFactory.ValidInput());
        var bornInactive = await EmployeeFactory.CreateAsync(hr, EmployeeFactory.ValidInput(status: "Inactive"));

        await Put(deactivated, "Inactive");
        await Put(deactivated, "Inactive");                   // already inactive: no second event
        await Put(renamed, "Active", "Renamed Still Active"); // active update: no event
        await Put(bornInactive, "Active");                    // reactivation: no event

        handler.Received.Should().Equal(deactivated.Id);

        async Task Put(EmployeeBody employee, string status, string? fullName = null)
        {
            var input = EmployeeFactory.ValidInput(fullName: fullName ?? employee.FullName, status: status) with { Email = employee.Email };
            await (await hr.PutAsJsonAsync($"/api/employees/{employee.Id}", input)).ShouldBeOkJsonAsync<EmployeeBody>();
        }
    }
}
