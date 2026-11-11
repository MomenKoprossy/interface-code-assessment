using BuildingBlocks.Domain;

namespace BuildingBlocks.Application;

public interface IDomainEventHandler<in TEvent>
    where TEvent : DomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken cancellationToken);
}

/// <summary>Invoked by AppDbContextBase after a successful save; a no-op when no handlers are registered.</summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyCollection<DomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
