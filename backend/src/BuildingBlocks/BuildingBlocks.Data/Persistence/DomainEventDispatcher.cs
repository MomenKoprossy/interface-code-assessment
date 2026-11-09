using System.Collections.Concurrent;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Data;

/// <summary>
/// Resolves and invokes the registered <see cref="IDomainEventHandler{TEvent}"/>(s) for each domain
/// event collected during SaveChanges. For every event its concrete runtime type closes the open
/// handler interface, all matching handlers are resolved from DI, and <c>Handle</c> is invoked.
/// No-op when no handlers are registered for an event.
/// </summary>
public sealed class DomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, HandlerInvoker> Invokers = new();

    public async Task DispatchAsync(
        IReadOnlyCollection<DomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            var invoker = Invokers.GetOrAdd(domainEvent.GetType(), HandlerInvoker.Create);

            foreach (var handler in invoker.ResolveHandlers(serviceProvider))
                await invoker.InvokeAsync(handler, domainEvent, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class HandlerInvoker
    {
        private readonly Type _handlerType;
        private readonly Func<object, DomainEvent, CancellationToken, Task> _invoke;

        private HandlerInvoker(Type handlerType, Func<object, DomainEvent, CancellationToken, Task> invoke)
        {
            _handlerType = handlerType;
            _invoke = invoke;
        }

        public static HandlerInvoker Create(Type eventType)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
            var method = handlerType.GetMethod(nameof(IDomainEventHandler<DomainEvent>.Handle))!;

            return new HandlerInvoker(
                handlerType,
                (handler, domainEvent, ct) => (Task)method.Invoke(handler, [domainEvent, ct])!);
        }

        public IEnumerable<object> ResolveHandlers(IServiceProvider serviceProvider) =>
            serviceProvider.GetServices(_handlerType).Where(h => h is not null)!;

        public Task InvokeAsync(object handler, DomainEvent domainEvent, CancellationToken ct) =>
            _invoke(handler, domainEvent, ct);
    }
}
