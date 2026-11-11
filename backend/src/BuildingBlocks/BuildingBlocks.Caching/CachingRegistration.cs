using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace BuildingBlocks.Caching;

public static class CachingRegistration
{
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DefaultLocalCacheExpiration = TimeSpan.FromMinutes(1);

    /// <summary>
    /// When a <c>cache</c> connection string exists (Aspire provides it for a referenced Redis resource), also
    /// registers the Redis <c>IDistributedCache</c>, which HybridCache picks up as its L2, and an
    /// <see cref="IConnectionMultiplexer"/> so Redis tracing can hook the connection.
    /// </summary>
    public static TBuilder AddPlatformCaching<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var services = builder.Services;
        var redis = builder.Configuration.GetConnectionString("cache");

        if (!string.IsNullOrWhiteSpace(redis))
        {
            services.AddStackExchangeRedisCache(o => o.Configuration = redis);
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redis));
        }

        services.AddHybridCache(o => o.DefaultEntryOptions = new HybridCacheEntryOptions
        {
            Expiration = DefaultExpiration,
            LocalCacheExpiration = DefaultLocalCacheExpiration,
        });

        return builder;
    }
}
