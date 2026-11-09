using System.Linq.Expressions;

namespace BuildingBlocks.Abstractions;

/// <summary>
/// Generic repository over an aggregate. Keyed on the interface (not a concrete base) so any TId works.
/// Implemented by EfRepository&lt;,&gt; in BuildingBlocks.Data.
/// </summary>
public interface IRepository<TEntity, in TId>
    where TEntity : class
{
    Task<TEntity?> FindAsync(TId id, CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> WhereAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
    Task<PagedList<TEntity>> PageAsync(PageRequest page, CancellationToken ct = default);
    Task<bool> ExistsAsync(TId id, CancellationToken ct = default);
    Task AddAsync(TEntity entity, CancellationToken ct = default);
    void Update(TEntity entity);
    void Remove(TEntity entity);
    IQueryable<TEntity> Query();
}

/// <summary>Commits the current unit of work; the SaveChanges call that stamps audit + dispatches domain events.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
