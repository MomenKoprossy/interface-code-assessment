using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Application;

/// <summary>
/// ~40-line hand-rolled dispatcher (spec §6) — resolves the IRequestHandler&lt;,&gt; for a request from DI
/// and wraps it in the ordered IPipelineBehavior&lt;,&gt; list. Zero third-party pipeline dependency.
/// Wrapper types are cached per request type so dispatch is reflection-free after the first call.
/// </summary>
public sealed class Mediator(IServiceProvider provider) : ISender
{
    private static readonly ConcurrentDictionary<Type, RequestHandlerWrapper> Wrappers = new();

    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = (RequestHandlerWrapper<TResponse>)Wrappers.GetOrAdd(
            request.GetType(),
            requestType =>
            {
                var wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, typeof(TResponse));
                return (RequestHandlerWrapper)Activator.CreateInstance(wrapperType)!;
            });

        return wrapper.Handle(request, provider, cancellationToken);
    }
}

internal abstract class RequestHandlerWrapper;

internal abstract class RequestHandlerWrapper<TResponse> : RequestHandlerWrapper
{
    public abstract ValueTask<TResponse> Handle(object request, IServiceProvider provider, CancellationToken cancellationToken);
}

internal sealed class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    public override ValueTask<TResponse> Handle(object request, IServiceProvider provider, CancellationToken cancellationToken)
    {
        var handler = provider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();

        MessageHandlerDelegate<TRequest, TResponse> pipeline = handler.Handle;

        // Wrap inner-to-outer so the FIRST-registered behavior runs OUTERMOST.
        var behaviors = provider.GetServices<IPipelineBehavior<TRequest, TResponse>>().ToArray();
        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var next = pipeline;
            pipeline = (req, token) => behavior.Handle(req, next, token);
        }

        return pipeline((TRequest)request, cancellationToken);
    }
}
