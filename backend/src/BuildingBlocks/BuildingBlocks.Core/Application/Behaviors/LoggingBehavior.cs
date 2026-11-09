using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application;

/// <summary>Logs handling + elapsed time for every request, correlated by trace id.</summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async ValueTask<TResponse> Handle(TRequest request, MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Handling {RequestName}", requestName);

        var sw = Stopwatch.GetTimestamp();
        var response = await next(request, cancellationToken);
        var elapsedMs = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;

        logger.LogInformation("Handled {RequestName} in {ElapsedMs:0.0} ms", requestName, elapsedMs);
        return response;
    }
}
