namespace BuildingBlocks.Abstractions;

/// <summary>Entities opt INTO auditing; the EF interceptor stamps these (keys off the interface, not a base type).</summary>
public interface IAuditable
{
    DateTime? CreatedAtUtc { get; set; }
    Guid? CreatedBy { get; set; }
    DateTime? ModifiedAtUtc { get; set; }
    Guid? ModifiedBy { get; set; }
}

/// <summary>Entities opt INTO soft-delete; the EF interceptor converts deletes to flag updates + applies a global filter.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
    Guid? DeletedBy { get; set; }
}
