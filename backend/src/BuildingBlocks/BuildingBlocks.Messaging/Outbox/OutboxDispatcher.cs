using System.Text.Json;
using BuildingBlocks.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Relays pending <see cref="OutboxMessage"/>s to the real transport (Rebus, or no-op without a broker) in
/// commit order, at-least-once — subscribers must be idempotent. A transport failure stops the batch and the
/// message stays pending for the next poll; a message that can't be deserialized is retired with an
/// <see cref="OutboxMessage.Error"/> so it doesn't block the queue.
/// </summary>
/// <remarks>
/// ponytail: single-instance poller — two replicas may both publish a row (still at-least-once); claim rows
/// with <c>FOR UPDATE SKIP LOCKED</c> if a service scales out. Processed rows are kept; add a retention
/// delete if the table grows.
/// </remarks>
internal sealed class OutboxDispatcher(
    IServiceScopeFactory scopes,
    [FromKeyedServices(MessagingRegistration.TransportKey)] IEventBus transport,
    ILogger<OutboxDispatcher> logger)
    : BackgroundService
{
    internal static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    internal const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            try
            {
                await DispatchPendingAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested) // incl. DB/transport timeouts — never stop the host
            {
                logger.LogError(ex, "Outbox dispatch cycle failed; retrying on next poll.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    /// <summary>Publishes one batch of pending messages; returns how many were retired (sent or poisoned).</summary>
    internal async Task<int> DispatchPendingAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var batch = await db.Set<OutboxMessage>()
            .Where(m => m.ProcessedAtUtc == null)
            .OrderBy(m => m.OccurredAtUtc)
            .Take(BatchSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var retired = 0;
        foreach (var message in batch)
        {
            var @event = Deserialize(message);
            if (@event is null)
            {
                logger.LogError("Outbox message {Id} can't be deserialized as {Type}; retiring it.", message.Id, message.Type);
                message.Error = $"Cannot deserialize as '{message.Type}'.";
                message.ProcessedAtUtc = clock.UtcNow;
                retired++;
                continue;
            }

            try
            {
                await transport.PublishAsync(@event, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Publishing outbox message {Id} ({Type}) failed; will retry.", message.Id, message.Type);
                message.Error = Truncate(ex.Message);
                break; // transport is likely down — keep order, retry the rest next poll
            }

            message.Error = null;
            message.ProcessedAtUtc = clock.UtcNow;
            retired++;
        }

        // Not cancellable: once messages are sent, record it even during shutdown (else the batch is re-sent).
        if (batch.Count > 0)
            await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);

        return retired;
    }

    private static IIntegrationEvent? Deserialize(OutboxMessage message)
    {
        var type = Type.GetType(message.Type, throwOnError: false);
        if (type is null || !typeof(IIntegrationEvent).IsAssignableFrom(type))
            return null;

        try
        {
            return JsonSerializer.Deserialize(message.Payload, type) as IIntegrationEvent;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Truncate(string value) => value.Length <= 2048 ? value : value[..2048];
}
