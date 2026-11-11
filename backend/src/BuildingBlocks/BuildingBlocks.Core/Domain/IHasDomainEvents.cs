namespace BuildingBlocks.Domain;

/// <summary>Non-generic so the EF SaveChanges hook can collect domain events without knowing the entity's TId.</summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<DomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
