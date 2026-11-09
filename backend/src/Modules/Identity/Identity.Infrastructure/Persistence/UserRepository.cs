using Identity.Application;
using Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

internal sealed class UserRepository(DbContext db) : IUserRepository
{
    private DbSet<User> Users => db.Set<User>();

    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

    public async Task<User?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        await Users.FindAsync([id], cancellationToken);

    public Task<bool> AnyAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        await Users.AddAsync(user, cancellationToken);
}
