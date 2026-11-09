using System.Linq.Expressions;
using BuildingBlocks.Abstractions;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BuildingBlocks.Data;

/// <summary>
/// Provider-agnostic EF Core base context. On <see cref="SaveChangesAsync(CancellationToken)"/> it
/// stamps <see cref="IAuditable"/> entities, converts <see cref="ISoftDeletable"/> deletes to flag
/// updates, collects domain events from <see cref="IHasDomainEvents"/> aggregates, saves, then
/// dispatches the collected events. <see cref="OnModelCreating"/> applies a global soft-delete query
/// filter to every <see cref="ISoftDeletable"/> entity type. The concrete DB provider (e.g. Npgsql)
/// is selected by the consuming SERVICE, not here.
/// </summary>
/// <remarks>
/// Derived contexts must apply their mappings (for example via <see cref="IModuleModelConfiguration"/>) BEFORE
/// calling <c>base.OnModelCreating(modelBuilder)</c>: the soft-delete filters and domain-event ignores are applied
/// only to entity types already in the model.
/// </remarks>
public abstract class AppDbContextBase : DbContext
{
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IDomainEventDispatcher? _dispatcher;

    protected AppDbContextBase(
        DbContextOptions options,
        ICurrentUser currentUser,
        IClock clock,
        IDomainEventDispatcher? dispatcher = null)
        : base(options)
    {
        _currentUser = currentUser;
        _clock = clock;
        _dispatcher = dispatcher;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = _clock.UtcNow;
        var userId = _currentUser.Id;

        StampAuditable(utcNow, userId);
        ConvertSoftDeletes(utcNow, userId);

        var domainEvents = CollectDomainEvents();

        var result = await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (domainEvents.Count > 0 && _dispatcher is not null)
            await _dispatcher.DispatchAsync(domainEvents, cancellationToken).ConfigureAwait(false);

        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        IgnoreDomainEvents(modelBuilder);
        ApplySoftDeleteQueryFilters(modelBuilder);
    }

    // Domain events are an in-memory concern dispatched on SaveChanges — never persisted. Ignore the
    // base Entity<TId>.DomainEvents collection on every aggregate so EF doesn't try to map DomainEvent
    // (which has no key). Done once here so no entity configuration has to repeat it.
    private static void IgnoreDomainEvents(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            if (typeof(IHasDomainEvents).IsAssignableFrom(entityType.ClrType))
                modelBuilder.Entity(entityType.ClrType).Ignore(nameof(IHasDomainEvents.DomainEvents));
        }
    }

    private void StampAuditable(DateTime utcNow, Guid? userId)
    {
        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = utcNow;
                    entry.Entity.CreatedBy = userId;
                    break;
                case EntityState.Modified:
                    entry.Entity.ModifiedAtUtc = utcNow;
                    entry.Entity.ModifiedBy = userId;
                    break;
            }
        }
    }

    private void ConvertSoftDeletes(DateTime utcNow, Guid? userId)
    {
        foreach (var entry in ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State != EntityState.Deleted)
                continue;

            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAtUtc = utcNow;
            entry.Entity.DeletedBy = userId;
        }
    }

    private IReadOnlyCollection<DomainEvent> CollectDomainEvents()
    {
        var aggregates = ChangeTracker.Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToArray();

        if (aggregates.Length == 0)
            return [];

        var events = aggregates.SelectMany(a => a.DomainEvents).ToArray();

        foreach (var aggregate in aggregates)
            aggregate.ClearDomainEvents();

        return events;
    }

    private static void ApplySoftDeleteQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
                continue;

            // Build: e => !((ISoftDeletable)e).IsDeleted
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var isDeletedProperty = Expression.Property(
                Expression.Convert(parameter, typeof(ISoftDeletable)),
                nameof(ISoftDeletable.IsDeleted));
            var filter = Expression.Lambda(Expression.Not(isDeletedProperty), parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }
}
