using BuildingBlocks.Abstractions;
using Rebus.Bus;

namespace BuildingBlocks.Messaging;

internal sealed class RebusEventBus(IBus bus) : IEventBus
{
    public Task PublishAsync(IIntegrationEvent message, CancellationToken ct = default) =>
        bus.Publish(message);
}
