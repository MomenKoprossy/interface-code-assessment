using BuildingBlocks.Abstractions;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Publishes cross-service integration events. Implementations route the event to the async
/// transport (RabbitMQ via Rebus) when configured, or no-op in dev when no broker is present.
/// Subscribers handle events by implementing Rebus <c>IHandleMessages&lt;TEvent&gt;</c>
/// (from <c>Rebus.Handlers</c>); event contracts are plain records implementing
/// <see cref="IIntegrationEvent"/> in a shared <c>*.Contracts</c> project.
/// With <c>RebusMessagingOptions.UseOutbox</c> the publish is staged in the service's DB and only sent after
/// the caller's <c>SaveChangesAsync</c> commits — so publish before saving.
/// </summary>
public interface IEventBus
{
    Task PublishAsync(IIntegrationEvent message, CancellationToken ct = default);
}
