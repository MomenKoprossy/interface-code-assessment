namespace BuildingBlocks.Domain;

/// <summary>
/// Non-generic view of an entity's domain events, so infrastructure (the EF SaveChanges hook in
/// BuildingBlocks.Data) can collect and dispatch them without knowing the entity's TId.
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<DomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
