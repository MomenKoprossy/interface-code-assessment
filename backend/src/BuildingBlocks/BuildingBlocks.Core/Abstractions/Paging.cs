namespace BuildingBlocks.Abstractions;

public sealed record PagedList<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    public static PagedList<T> Empty(int page = 1, int pageSize = 20) =>
        new([], page, pageSize, 0);
}

/// <summary>A normalized paging request (clamped to sane bounds).</summary>
public readonly record struct PageRequest
{
    public int Page { get; }
    public int PageSize { get; }

    public PageRequest(int page = 1, int pageSize = 20, int maxPageSize = 100)
    {
        Page = page < 1 ? 1 : page;
        PageSize = pageSize < 1 ? 20 : (pageSize > maxPageSize ? maxPageSize : pageSize);
    }

    public int Skip => (Page - 1) * PageSize;
    public int Take => PageSize;
}
