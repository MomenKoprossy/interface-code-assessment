using FluentValidation;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application;

/// <summary>Logs unexpected exceptions from handlers (validation failures are expected, so skipped),
/// then rethrows so the web layer maps them to RFC 7807 ProblemDetails (spec §6).</summary>
public sealed class UnhandledExceptionBehavior<TRequest, TResponse>(ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async ValueTask<TResponse> Handle(TRequest request, MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not ValidationException)
        {
            logger.LogError(ex, "Unhandled exception handling {RequestName}", typeof(TRequest).Name);
            throw;
        }
    }
}
