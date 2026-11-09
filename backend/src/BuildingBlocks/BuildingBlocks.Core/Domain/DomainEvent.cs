namespace BuildingBlocks.Domain;

/// <summary>
/// In-process notification raised by an aggregate and dispatched on SaveChanges (after the audit
/// interceptor runs). Concrete events are records: <c>record EmployeeDeactivated(...) : DomainEvent;</c>
/// </summary>
public abstract record DomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
