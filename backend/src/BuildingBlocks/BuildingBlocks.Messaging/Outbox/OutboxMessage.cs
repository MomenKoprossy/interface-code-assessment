using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Saved in the same transaction as the business change that raised it. Pending while
/// <see cref="ProcessedAtUtc"/> is null; <see cref="Error"/> holds the last failure for inspection.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public string Type { get; init; } = default!;    // "Namespace.TypeName, AssemblyName" (version-free)
    public string Payload { get; init; } = default!; // System.Text.Json
    public DateTime OccurredAtUtc { get; init; }
    public DateTime? ProcessedAtUtc { get; set; }
    public string? Error { get; set; }
}

public static class OutboxModelBuilderExtensions
{
    /// <summary>
    /// Maps into the calling context's default schema. Call from <c>OnModelCreating</c> of any service that sets
    /// <c>RebusMessagingOptions.UseOutbox</c>, then add a migration.
    /// </summary>
    public static ModelBuilder AddPlatformOutbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox_messages");
            b.HasKey(x => x.Id);
            b.Property(x => x.Type).HasMaxLength(512).IsRequired();
            b.Property(x => x.Payload).IsRequired();
            b.Property(x => x.Error).HasMaxLength(2048);
            b.HasIndex(x => new { x.ProcessedAtUtc, x.OccurredAtUtc });
        });
        return modelBuilder;
    }
}
