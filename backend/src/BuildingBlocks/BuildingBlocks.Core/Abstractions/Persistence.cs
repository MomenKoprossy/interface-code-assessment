using System.Linq.Expressions;

namespace BuildingBlocks.Abstractions;

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

/// <summary>Saving stamps audit fields and dispatches domain events.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
