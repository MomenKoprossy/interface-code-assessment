using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Application;

public static class MediatorRegistration
{
    /// <summary>
    /// Registers the hand-rolled mediator: ISender, the ordered pipeline behaviors (logging → validation →
    /// handler), every IRequestHandler&lt;,&gt; and IValidator in the given assemblies. Called by AddPlatform()
    /// (BuildingBlocks.Web) with the service's handler assemblies. Exceptions are classified and logged once, by
    /// the web layer's GlobalExceptionHandler, not here.
    /// </summary>
    public static IServiceCollection AddPlatformMediator(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
            assemblies = [Assembly.GetCallingAssembly()];

        services.AddScoped<ISender, Mediator>();

        // Order matters: first registered runs outermost (see Mediator dispatch).
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        foreach (var assembly in assemblies.Distinct())
        {
            foreach (var (service, implementation) in FindHandlers(assembly))
                services.AddScoped(service, implementation);

            services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        }

        return services;
    }

    private static IEnumerable<(Type Service, Type Implementation)> FindHandlers(Assembly assembly) =>
        from type in assembly.GetTypes()
        where type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false }
        from iface in type.GetInterfaces()
        where iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)
        select (iface, type);
}
