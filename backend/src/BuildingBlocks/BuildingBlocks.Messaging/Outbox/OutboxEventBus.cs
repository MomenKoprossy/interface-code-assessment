using System.Text.Json;
using BuildingBlocks.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Outbox-backed <see cref="IEventBus"/> (registered when <c>UseOutbox</c> is set): <c>PublishAsync</c> only
/// stages an <see cref="OutboxMessage"/> on the scoped <see cref="DbContext"/>, so the event commits atomically
/// with the caller's next <c>SaveChangesAsync</c>. Publish BEFORE saving — an event staged after the last save is never sent.
/// </summary>
internal sealed class OutboxEventBus(DbContext db, IClock clock) : IEventBus
{
    public Task PublishAsync(IIntegrationEvent message, CancellationToken ct = default)
    {
        var type = message.GetType();
        db.Set<OutboxMessage>().Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = $"{type.FullName}, {type.Assembly.GetName().Name}",
            Payload = JsonSerializer.Serialize(message, type),
            OccurredAtUtc = clock.UtcNow,
        });
        return Task.CompletedTask;
    }
}
