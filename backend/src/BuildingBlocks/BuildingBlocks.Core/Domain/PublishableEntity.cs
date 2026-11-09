using BuildingBlocks.Abstractions;

namespace BuildingBlocks.Domain;

public enum PublishStatus { Draft = 0, Published = 1, Archived = 2 }

/// <summary>
/// Base for any content that can be published/unpublished (spec §4.2). CMS entities inherit this and
/// get Draft/Published/Archived + audit + soft-delete for free. Publishing records when and raises a
/// domain event others can react to.
/// </summary>
public abstract class PublishableEntity<TId> : AggregateRoot<TId>, IAuditable, ISoftDeletable
{
    public PublishStatus Status { get; private set; } = PublishStatus.Draft;
    public DateTime? PublishedAtUtc { get; private set; }

    public bool IsPublished => Status == PublishStatus.Published;

    public void Publish(DateTime utcNow)
    {
        if (Status == PublishStatus.Published) return;          // idempotent
        Status = PublishStatus.Published;
        PublishedAtUtc = utcNow;
        Raise(new EntityPublished(GetType().Name, Id!.ToString()!));
    }

    public void Unpublish()
    {
        if (Status != PublishStatus.Published) return;          // idempotent
        Status = PublishStatus.Draft;
        PublishedAtUtc = null;
        Raise(new EntityUnpublished(GetType().Name, Id!.ToString()!));
    }

    public void Archive() => Status = PublishStatus.Archived;

    // IAuditable / ISoftDeletable — stamped automatically by the EF interceptor (BuildingBlocks.Data).
    public DateTime? CreatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public Guid? ModifiedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }
}

public sealed record EntityPublished(string EntityType, string EntityId) : DomainEvent;
public sealed record EntityUnpublished(string EntityType, string EntityId) : DomainEvent;
