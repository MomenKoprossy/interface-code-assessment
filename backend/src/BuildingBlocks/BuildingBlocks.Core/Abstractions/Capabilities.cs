namespace BuildingBlocks.Abstractions;

/// <summary>Entities opt into auditing; SaveChanges stamps these (keyed off the interface, not a base type).</summary>
public interface IAuditable
{
    DateTime? CreatedAtUtc { get; set; }
    Guid? CreatedBy { get; set; }
    DateTime? ModifiedAtUtc { get; set; }
    Guid? ModifiedBy { get; set; }
}

/// <summary>Entities opt into soft delete; SaveChanges turns deletes into flag updates and a global query filter hides them.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
    Guid? DeletedBy { get; set; }
}
