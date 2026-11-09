using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Grpc;

/// <summary>
/// Composition helpers for contract-first gRPC: a server registration and a typed-client
/// registration bound to a logical service-discovery name with standard resilience.
/// </summary>
public static class GrpcServiceCollectionExtensions
{
    /// <summary>
    /// Registers the gRPC server stack. Services map their generated implementations
    /// themselves via <c>app.MapGrpcService&lt;TImpl&gt;()</c>.
    /// </summary>
    public static IServiceCollection AddPlatformGrpcServer(this IServiceCollection services)
    {
        services.AddGrpc();
        return services;
    }

    /// <summary>
    /// Registers a typed gRPC client bound to a logical service name (e.g. <c>"attachment"</c>),
    /// resolved at runtime by Aspire service discovery, with the standard resilience handler.
    /// </summary>
    /// <typeparam name="TClient">The generated gRPC client type (e.g. <c>AttachmentGrpc.AttachmentGrpcClient</c>).</typeparam>
    /// <param name="logicalName">The logical service-discovery name (the host part of <c>http://{logicalName}</c>).</param>
    public static IHttpClientBuilder AddPlatformGrpcClient<TClient>(this IServiceCollection services, string logicalName)
        where TClient : class
    {
        IHttpClientBuilder builder = services
            .AddGrpcClient<TClient>(o => o.Address = new Uri($"http://{logicalName}"))
            .AddServiceDiscovery();

        // AddStandardResilienceHandler returns IHttpStandardResiliencePipelineBuilder (for further
        // pipeline tuning); we expose the underlying IHttpClientBuilder per the platform contract.
        builder.AddStandardResilienceHandler();
        return builder;
    }
}
