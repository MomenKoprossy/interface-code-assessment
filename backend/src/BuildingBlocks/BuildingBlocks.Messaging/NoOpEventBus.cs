using BuildingBlocks.Abstractions;

namespace BuildingBlocks.Messaging;

internal sealed class NoOpEventBus : IEventBus
{
    public Task PublishAsync(IIntegrationEvent message, CancellationToken ct = default) =>
        Task.CompletedTask;
}
