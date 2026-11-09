using AwesomeAssertions;
using BuildingBlocks.Messaging;
using Rebus.Handlers;

namespace BuildingBlocks.UnitTests.Messaging;

/// <summary>
/// Over RabbitMQ a subscriber's queue only receives an event after <c>bus.Subscribe(type)</c>; AddPlatformMessaging
/// subscribes to every integration event a registered handler handles (and nothing else).
/// </summary>
public class SubscriptionTests
{
    private sealed class SomethingHappenedHandler : IHandleMessages<SomethingHappened>
    {
        public Task Handle(SomethingHappened message) => Task.CompletedTask;
    }

    private sealed class NotAnIntegrationEventHandler : IHandleMessages<string>
    {
        public Task Handle(string message) => Task.CompletedTask;
    }

    [Fact]
    public void Subscribes_to_integration_events_with_a_handler()
    {
        MessagingRegistration.FindSubscribedEventTypes([typeof(SubscriptionTests).Assembly, typeof(SubscriptionTests).Assembly])
            .Should().Equal(typeof(SomethingHappened));
    }
}
