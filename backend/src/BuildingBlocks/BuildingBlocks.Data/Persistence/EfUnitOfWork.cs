using BuildingBlocks.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Data;

/// <summary>
/// EF Core <see cref="IUnitOfWork"/> — delegates to the resolved <see cref="DbContext"/>'s
/// SaveChanges, which (via <see cref="AppDbContextBase"/>) stamps audit, applies soft-delete, and
/// dispatches domain events.
/// </summary>
public sealed class EfUnitOfWork(DbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
