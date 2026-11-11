namespace BuildingBlocks.Domain;

/// <summary>In-process notification, dispatched on SaveChanges after the audit fields are stamped.</summary>
public abstract record DomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
