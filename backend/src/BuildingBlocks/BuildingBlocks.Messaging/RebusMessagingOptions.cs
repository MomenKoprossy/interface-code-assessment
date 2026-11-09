using System.Reflection;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Options for <c>AddPlatformMessaging</c>. A publish-only service still needs a
/// <see cref="QueueName"/> (the RabbitMQ transport requires an input queue even with no
/// subscriptions). Add the assemblies that contain Rebus <c>IHandleMessages&lt;T&gt;</c>
/// implementations to <see cref="HandlerAssemblies"/> so they are auto-discovered.
/// </summary>
public sealed class RebusMessagingOptions
{
    /// <summary>The Rebus input queue name for this service (e.g. "cms", "attachment").</summary>
    public string QueueName { get; set; } = "";

    /// <summary>Assemblies scanned for Rebus <c>IHandleMessages&lt;T&gt;</c> handlers.</summary>
    public List<Assembly> HandlerAssemblies { get; } = [];

    /// <summary>
    /// Transactional outbox: <c>IEventBus.PublishAsync</c> stages the event on the service's scoped
    /// <c>DbContext</c> so it commits with the business change, and a background dispatcher relays it.
    /// The context must call <c>modelBuilder.AddPlatformOutbox()</c> (plus a migration), and handlers must
    /// publish BEFORE <c>SaveChangesAsync</c>.
    /// </summary>
    public bool UseOutbox { get; set; }
}
