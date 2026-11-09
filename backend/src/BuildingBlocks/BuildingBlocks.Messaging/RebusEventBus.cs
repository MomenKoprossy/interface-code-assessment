using BuildingBlocks.Abstractions;
using Rebus.Bus;

namespace BuildingBlocks.Messaging;

/// <summary>
/// <see cref="IEventBus"/> backed by Rebus. Publishes the integration event onto the bus;
/// Rebus routes it to subscribers over the configured RabbitMQ transport.
/// </summary>
internal sealed class RebusEventBus(IBus bus) : IEventBus
{
    public Task PublishAsync(IIntegrationEvent message, CancellationToken ct = default) =>
        bus.Publish(message);
}
