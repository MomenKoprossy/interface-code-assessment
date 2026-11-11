using BuildingBlocks.Abstractions;

namespace BuildingBlocks.Application;

public interface IRequest<out TResponse>;

public interface ICommand<out TResponse> : IRequest<TResponse>;

public interface IQuery<out TResponse> : IRequest<TResponse>;

/// <summary>The next step in the pipeline (the wrapped handler or the next behavior).</summary>
public delegate ValueTask<TResponse> MessageHandlerDelegate<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken);

public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    ValueTask<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

public interface IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    ValueTask<TResponse> Handle(TRequest request, MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken);
}

public interface ISender
{
    ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
