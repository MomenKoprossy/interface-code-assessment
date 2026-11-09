using AwesomeAssertions;
using BuildingBlocks.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace BuildingBlocks.UnitTests.Caching;

public class CachingRegistrationTests
{
    [Fact]
    public async Task Without_a_cache_connection_string_HybridCache_is_in_memory_only()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.AddPlatformCaching();
        await using var services = builder.Services.BuildServiceProvider();

        var cache = services.GetRequiredService<HybridCache>();
        var value = await cache.GetOrCreateAsync("key", _ => ValueTask.FromResult(42),
            cancellationToken: TestContext.Current.CancellationToken);

        value.Should().Be(42);
        services.GetService<IConnectionMultiplexer>().Should().BeNull();
        services.GetService<IDistributedCache>().Should().BeNull();
    }

    [Fact]
    public void With_a_cache_connection_string_Redis_becomes_the_L2()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration["ConnectionStrings:cache"] = "localhost:6379";
        builder.AddPlatformCaching();

        builder.Services.Should().Contain(d => d.ServiceType == typeof(IDistributedCache))
            .And.Contain(d => d.ServiceType == typeof(IConnectionMultiplexer));
    }
}
