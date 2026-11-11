using BuildingBlocks.Abstractions;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Data;

public static class RepositoryRegistration
{
    /// <summary>
    /// Aliases <see cref="DbContext"/> to <typeparamref name="TContext"/> so the repository and unit of work can
    /// depend on the base type. <typeparamref name="TContext"/> itself must be registered separately
    /// (e.g. <c>AddDbContext</c>).
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
