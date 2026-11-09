using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace BuildingBlocks.Caching;

/// <summary>Registers HybridCache (L1 in-memory by default, Redis L2 only when configured).</summary>
public static class CachingRegistration
{
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DefaultLocalCacheExpiration = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Adds <c>HybridCache</c> with platform default entry options (overall expiration 5 min, local
    /// L1 expiration 1 min). If a connection string named <c>cache</c> exists (Aspire provides it when
    /// a Redis resource is referenced) it also registers the Redis <c>IDistributedCache</c> — which
    /// HybridCache automatically promotes to its L2 backplane — and an <see cref="IConnectionMultiplexer"/>
    /// so Redis tracing can hook the connection.
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
