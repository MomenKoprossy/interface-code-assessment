using System.Reflection;

namespace BuildingBlocks.Messaging;

/// <summary>
/// A publish-only service still needs a <see cref="QueueName"/>: the RabbitMQ transport requires an input queue
/// even with no subscriptions.
/// </summary>
public sealed class RebusMessagingOptions
{
    public string QueueName { get; set; } = "";

    public List<Assembly> HandlerAssemblies { get; } = [];

    /// <summary>
    /// Transactional outbox: <c>IEventBus.PublishAsync</c> stages the event on the service's scoped
    /// <c>DbContext</c> so it commits with the business change, and a background dispatcher relays it.
    /// The context must call <c>modelBuilder.AddPlatformOutbox()</c> (plus a migration), and handlers must
    /// publish BEFORE <c>SaveChangesAsync</c>.
    /// </summary>
    public bool UseOutbox { get; set; }
}
