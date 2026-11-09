using BuildingBlocks.Abstractions;

namespace BuildingBlocks.Application;

/// <summary>A request with a response — the base both commands and queries specialize.</summary>
public interface IRequest<out TResponse>;

/// <summary>A state-changing request. Typically <c>ICommand&lt;Result&gt;</c> or <c>ICommand&lt;Result&lt;T&gt;&gt;</c>.</summary>
public interface ICommand<out TResponse> : IRequest<TResponse>;

/// <summary>A read-only request. Typically <c>IQuery&lt;Result&lt;T&gt;&gt;</c>.</summary>
public interface IQuery<out TResponse> : IRequest<TResponse>;

/// <summary>The next step in the pipeline (the wrapped handler or the next behavior).</summary>
public delegate ValueTask<TResponse> MessageHandlerDelegate<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken);

/// <summary>Handles exactly one request type. One handler per command/query.</summary>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    ValueTask<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

/// <summary>Cross-cutting step wrapping every handler (validation, logging, exceptions, caching).</summary>
public interface IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    ValueTask<TResponse> Handle(TRequest request, MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken);
}

/// <summary>Sends a request through the pipeline to its handler.</summary>
public interface ISender
{
    ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
