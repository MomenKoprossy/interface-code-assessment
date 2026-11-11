using BuildingBlocks.Abstractions;

namespace BuildingBlocks.Application;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
