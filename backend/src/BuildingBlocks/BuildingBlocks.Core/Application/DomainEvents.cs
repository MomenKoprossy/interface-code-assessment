using BuildingBlocks.Domain;

namespace BuildingBlocks.Application;

/// <summary>Handles an in-process domain event (e.g. react to EntityPublished). Optional per service.</summary>
public interface IDomainEventHandler<in TEvent>
    where TEvent : DomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken cancellationToken);
}

/// <summary>
/// Dispatches domain events collected during SaveChanges to their handlers. Implemented in
/// BuildingBlocks.Data and invoked by AppDbContextBase after a successful save. No-op when no
/// handlers are registered.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyCollection<DomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
