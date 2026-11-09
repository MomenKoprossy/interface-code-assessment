using AwesomeAssertions;
using BuildingBlocks.Abstractions;
using BuildingBlocks.Messaging;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace BuildingBlocks.UnitTests.Messaging;

public sealed record SomethingHappened(Guid Id) : IIntegrationEvent;

/// <summary>
/// Transactional outbox over a real relational provider (SQLite in-memory): a publish is staged and only
/// persists with the caller's SaveChanges; the dispatcher relays pending messages in order, keeps them pending
/// when the transport fails (stopping the batch), and retires ones it can't deserialize.
/// </summary>
public sealed class OutboxTests : IAsyncLifetime
{
    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.AddPlatformOutbox();
    }

    private sealed class SteppingClock : IClock
    {
        private DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public DateTime UtcNow => _now = _now.AddSeconds(1);
    }

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly IEventBus _transport = Substitute.For<IEventBus>();
    private readonly IClock _clock = new SteppingClock();
    private ServiceProvider _services = default!;
    private OutboxDispatcher _dispatcher = default!;
    private Exception? _transportError;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync(Ct);
        _services = new ServiceCollection()
            .AddDbContext<TestDbContext>(o => o.UseSqlite(_connection))
            .AddScoped<DbContext>(sp => sp.GetRequiredService<TestDbContext>())
            .AddSingleton(_clock)
            .BuildServiceProvider();

        await using (var scope = _services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<DbContext>().Database.EnsureCreatedAsync(Ct);

        _transport.PublishAsync(default!, default).ReturnsForAnyArgs(_ =>
            _transportError is { } error ? Task.FromException(error) : Task.CompletedTask);
        _dispatcher = new OutboxDispatcher(
            _services.GetRequiredService<IServiceScopeFactory>(), _transport, NullLogger<OutboxDispatcher>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task PublishAsync(params IIntegrationEvent[] events)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        var bus = new OutboxEventBus(db, _clock);
        var before = await CountAsync();

        foreach (var e in events)
            await bus.PublishAsync(e, Ct);

        (await CountAsync()).Should().Be(before, "a staged event must not persist before SaveChanges");
        await db.SaveChangesAsync(Ct);
        (await CountAsync()).Should().Be(before + events.Length);
    }

    private async Task<List<OutboxMessage>> MessagesAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DbContext>()
            .Set<OutboxMessage>().AsNoTracking().OrderBy(m => m.OccurredAtUtc).ToListAsync(Ct);
    }

    private async Task<int> CountAsync() => (await MessagesAsync()).Count;

    [Fact]
    public async Task Staged_events_commit_with_SaveChanges_and_dispatch_in_order()
    {
        var first = new SomethingHappened(Guid.NewGuid());
        var second = new SomethingHappened(Guid.NewGuid());
        await PublishAsync(first, second);

        (await _dispatcher.DispatchPendingAsync(Ct)).Should().Be(2);

        Received.InOrder(() =>
        {
            _transport.PublishAsync(Arg.Is<SomethingHappened>(e => e.Id == first.Id), Arg.Any<CancellationToken>());
            _transport.PublishAsync(Arg.Is<SomethingHappened>(e => e.Id == second.Id), Arg.Any<CancellationToken>());
        });
        (await MessagesAsync()).Should().AllSatisfy(m =>
        {
            m.ProcessedAtUtc.Should().NotBeNull();
            m.Error.Should().BeNull();
        });
        (await _dispatcher.DispatchPendingAsync(Ct)).Should().Be(0, "processed messages are not re-sent");
    }

    [Fact]
    public async Task Transport_failure_keeps_messages_pending_and_stops_the_batch_until_it_recovers()
    {
        await PublishAsync(new SomethingHappened(Guid.NewGuid()), new SomethingHappened(Guid.NewGuid()));
        _transportError = new InvalidOperationException("broker down");

        (await _dispatcher.DispatchPendingAsync(Ct)).Should().Be(0);

        await _transport.ReceivedWithAnyArgs(1).PublishAsync(default!, default);
        var pending = await MessagesAsync();
        pending.Should().AllSatisfy(m => m.ProcessedAtUtc.Should().BeNull());
        pending[0].Error.Should().Be("broker down");

        _transportError = null;
        (await _dispatcher.DispatchPendingAsync(Ct)).Should().Be(2);
        (await MessagesAsync()).Should().AllSatisfy(m => m.Error.Should().BeNull());
    }

    [Fact]
    public async Task Transport_timeout_is_treated_as_a_failure_not_a_shutdown()
    {
        await PublishAsync(new SomethingHappened(Guid.NewGuid()));
        _transportError = new TaskCanceledException("publish timed out");

        (await _dispatcher.DispatchPendingAsync(Ct)).Should().Be(0, "a timeout must not escape and stop the host");

        (await MessagesAsync()).Single().ProcessedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Undeserializable_message_is_retired_with_an_error_without_blocking_the_queue()
    {
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbContext>();
            db.Set<OutboxMessage>().Add(new OutboxMessage
            {
                Id = Guid.NewGuid(), Type = "Gone.Event, Gone", Payload = "{}", OccurredAtUtc = _clock.UtcNow,
            });
            await db.SaveChangesAsync(Ct);
        }
        var good = new SomethingHappened(Guid.NewGuid());
        await PublishAsync(good);

        (await _dispatcher.DispatchPendingAsync(Ct)).Should().Be(2);

        await _transport.Received(1).PublishAsync(Arg.Is<SomethingHappened>(e => e.Id == good.Id), Arg.Any<CancellationToken>());
        var poisoned = (await MessagesAsync())[0];
        poisoned.ProcessedAtUtc.Should().NotBeNull();
        poisoned.Error.Should().Contain("Gone.Event");
    }
}
