using System.Reflection;
using BuildingBlocks.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.ServiceProvider;

namespace BuildingBlocks.Messaging;

public static class MessagingRegistration
{
    /// <summary>DI key of the real transport publisher (Rebus or no-op) that the outbox dispatcher relays to.</summary>
    internal const string TransportKey = "BuildingBlocks.Messaging.Transport";

    /// <summary>
    /// Uses Rebus over RabbitMQ when a <c>rabbit</c> connection string exists; otherwise registers a no-op
    /// <see cref="IEventBus"/> so the service still boots without a broker. With
    /// <see cref="RebusMessagingOptions.UseOutbox"/>, <see cref="IEventBus"/> stages events in the service's DB and a
    /// hosted dispatcher relays them to the transport.
    /// </summary>
    /// <remarks>
    /// Rebus 8 emits the "Rebus" <c>ActivitySource</c> on its own; the older
    /// <c>Options(o =&gt; o.EnableDiagnosticSources())</c> hook does not exist in Rebus 8.9.2, so it is not called.
    /// </remarks>
    public static TBuilder AddPlatformMessaging<TBuilder>(
        this TBuilder builder,
        Action<RebusMessagingOptions>? configure = null)
        where TBuilder : IHostApplicationBuilder
    {
        var options = new RebusMessagingOptions();
        configure?.Invoke(options);

        var services = builder.Services;
        var rabbitConn = builder.Configuration.GetConnectionString("rabbit");

        // No broker: Rebus must not be registered.
        if (string.IsNullOrWhiteSpace(rabbitConn))
        {
            services.TryAddKeyedSingleton<IEventBus, NoOpEventBus>(TransportKey);
        }
        else
        {
            // RabbitMq transport requires an input queue even for publish-only services. Subscribing binds this
            // queue to each handled event's topic — without it RabbitMQ silently drops the published events.
            var subscriptions = FindSubscribedEventTypes(options.HandlerAssemblies);
            services.AddRebus(
                cfg => cfg
                    .Transport(t => t.UseRabbitMq(rabbitConn, options.QueueName))
                    .Options(o => o.SetBusName(options.QueueName)),
                onCreated: async bus =>
                {
                    foreach (var eventType in subscriptions)
                        await bus.Subscribe(eventType).ConfigureAwait(false);
                });

            foreach (var assembly in options.HandlerAssemblies.Distinct())
                services.AutoRegisterHandlersFromAssembly(assembly);

            services.TryAddKeyedSingleton<IEventBus, RebusEventBus>(TransportKey);
        }

        if (options.UseOutbox)
        {
            // Requires a scoped DbContext (AddPlatformRepositories) that maps modelBuilder.AddPlatformOutbox().
            services.TryAddScoped<IEventBus, OutboxEventBus>();
            services.AddHostedService<OutboxDispatcher>();
        }
        else
        {
            services.TryAddSingleton<IEventBus>(sp => sp.GetRequiredKeyedService<IEventBus>(TransportKey));
        }

        return builder;
    }

    internal static Type[] FindSubscribedEventTypes(IEnumerable<Assembly> assemblies) =>
        assemblies.Distinct()
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHandleMessages<>))
            .Select(i => i.GetGenericArguments()[0])
            .Where(typeof(IIntegrationEvent).IsAssignableFrom)
            .Distinct()
            .ToArray();
}
