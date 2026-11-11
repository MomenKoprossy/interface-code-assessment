using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Grpc;

public static class GrpcServiceCollectionExtensions
{
    /// <summary>Services map their generated implementations themselves via <c>app.MapGrpcService&lt;TImpl&gt;()</c>.</summary>
    public static IServiceCollection AddPlatformGrpcServer(this IServiceCollection services)
    {
        services.AddGrpc();
        return services;
    }

    /// <param name="logicalName">Service-discovery name, resolved at runtime by Aspire (the host part of <c>http://{logicalName}</c>).</param>
    public static IHttpClientBuilder AddPlatformGrpcClient<TClient>(this IServiceCollection services, string logicalName)
        where TClient : class
    {
        IHttpClientBuilder builder = services
            .AddGrpcClient<TClient>(o => o.Address = new Uri($"http://{logicalName}"))
            .AddServiceDiscovery();

        // Return the client builder, not the resilience pipeline builder AddStandardResilienceHandler returns.
        builder.AddStandardResilienceHandler();
        return builder;
    }
}
