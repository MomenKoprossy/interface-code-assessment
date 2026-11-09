using BuildingBlocks.Abstractions;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Data;

/// <summary>Registers the EF persistence stack for a service's concrete <typeparamref name="TContext"/>.</summary>
public static class RepositoryRegistration
{
    /// <summary>
    /// Wires the generic persistence services around the service's own <typeparamref name="TContext"/>:
    /// <list type="bullet">
    /// <item>a scoped <see cref="DbContext"/> that resolves to <typeparamref name="TContext"/> so
    /// <see cref="EfRepository{TEntity, TId}"/> / <see cref="EfUnitOfWork"/> can depend on the base type;</item>
    /// <item><see cref="IUnitOfWork"/> → <see cref="EfUnitOfWork"/>;</item>
    /// <item>open-generic <see cref="IRepository{TEntity, TId}"/> → <see cref="EfRepository{TEntity, TId}"/>;</item>
    /// <item><see cref="IDomainEventDispatcher"/> → <see cref="DomainEventDispatcher"/>.</item>
    /// </list>
    /// The <typeparamref name="TContext"/> itself must be registered separately (e.g. via
    /// <c>AddDbContext&lt;TContext&gt;(o =&gt; o.UseNpgsql(...))</c> in the service).
    /// </summary>
    public static IServiceCollection AddPlatformRepositories<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<TContext>());
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped(typeof(IRepository<,>), typeof(EfRepository<,>));
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        return services;
    }
}
