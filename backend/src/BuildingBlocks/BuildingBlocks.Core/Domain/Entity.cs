namespace BuildingBlocks.Domain;

/// <summary>Base entity: identity + a buffer of domain events to be dispatched on SaveChanges.</summary>
public abstract class Entity<TId> : IHasDomainEvents
{
    private readonly List<DomainEvent> _domainEvents = [];

    public TId Id { get; protected set; } = default!;

    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    public override bool Equals(object? obj) =>
        obj is Entity<TId> other
        && other.GetType() == GetType()
        && !EqualityComparer<TId>.Default.Equals(Id, default)
        && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}

/// <summary>An aggregate root — the only entity type a repository is keyed on.</summary>
public abstract class AggregateRoot<TId> : Entity<TId>;
