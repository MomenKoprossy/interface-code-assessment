using BuildingBlocks.Abstractions;

namespace BuildingBlocks.Messaging;

/// <summary>
/// A no-op when no broker is configured. Subscribers implement Rebus <c>IHandleMessages&lt;TEvent&gt;</c>.
/// With <c>RebusMessagingOptions.UseOutbox</c> the publish is staged in the service's DB and only sent after
/// the caller's <c>SaveChangesAsync</c> commits, so publish before saving.
/// </summary>
public interface IEventBus
{
    Task PublishAsync(IIntegrationEvent message, CancellationToken ct = default);
}
