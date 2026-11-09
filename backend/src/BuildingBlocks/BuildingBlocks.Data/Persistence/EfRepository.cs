using System.Linq.Expressions;
using BuildingBlocks.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Data;

/// <summary>
/// Generic EF Core implementation of <see cref="IRepository{TEntity, TId}"/> over the resolved
/// <see cref="DbContext"/>. Registered open-generic by <c>AddPlatformRepositories</c> so any
/// aggregate gets a repository for free. Keys off <c>DbContext.Set&lt;TEntity&gt;()</c>.
/// </summary>
public sealed class EfRepository<TEntity, TId>(DbContext db) : IRepository<TEntity, TId>
    where TEntity : class
{
    private DbSet<TEntity> Set => db.Set<TEntity>();

    public async Task<TEntity?> FindAsync(TId id, CancellationToken ct = default) =>
        await Set.FindAsync([id], ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken ct = default) =>
        await Set.ToListAsync(ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<TEntity>> WhereAsync(
        Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default) =>
        await Set.Where(predicate).ToListAsync(ct).ConfigureAwait(false);

    public async Task<PagedList<TEntity>> PageAsync(PageRequest page, CancellationToken ct = default)
    {
        var total = await Set.LongCountAsync(ct).ConfigureAwait(false);
        var items = await Set
            .Skip(page.Skip)
            .Take(page.Take)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new PagedList<TEntity>(items, page.Page, page.PageSize, total);
    }

    public async Task<bool> ExistsAsync(TId id, CancellationToken ct = default) =>
        await Set.FindAsync([id], ct).ConfigureAwait(false) is not null;

    public async Task AddAsync(TEntity entity, CancellationToken ct = default) =>
        await Set.AddAsync(entity, ct).ConfigureAwait(false);

    public void Update(TEntity entity) => Set.Update(entity);

    public void Remove(TEntity entity) => Set.Remove(entity);

    public IQueryable<TEntity> Query() => Set.AsQueryable();
}
