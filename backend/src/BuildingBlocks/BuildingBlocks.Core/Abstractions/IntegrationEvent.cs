namespace BuildingBlocks.Abstractions;

/// <summary>
/// Marker for cross-service integration events (published over RabbitMQ via BuildingBlocks.Messaging).
/// Contracts are plain records living in a shared *.Contracts project.
/// </summary>
public interface IIntegrationEvent
{
}
