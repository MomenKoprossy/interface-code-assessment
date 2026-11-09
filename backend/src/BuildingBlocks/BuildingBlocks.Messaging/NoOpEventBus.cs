using BuildingBlocks.Abstractions;

namespace BuildingBlocks.Messaging;

/// <summary>
/// No-op <see cref="IEventBus"/> registered when no <c>rabbit</c> connection string is configured,
/// so a service still boots in dev without RabbitMQ. Publishing simply does nothing.
/// </summary>
internal sealed class NoOpEventBus : IEventBus
{
    public Task PublishAsync(IIntegrationEvent message, CancellationToken ct = default) =>
        Task.CompletedTask;
}
