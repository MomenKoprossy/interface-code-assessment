using BuildingBlocks.Abstractions;

namespace BuildingBlocks.Application;

/// <summary>Default <see cref="IClock"/> — registered as a singleton by AddPlatform().</summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
