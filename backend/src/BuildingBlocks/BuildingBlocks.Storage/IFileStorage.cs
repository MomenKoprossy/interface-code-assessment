namespace BuildingBlocks.Storage;

public interface IFileStorage
{
    Task<StoredFile> SaveAsync(Stream content, string fileName, string contentType, CancellationToken ct);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct);
    Task<string> GetDownloadUrlAsync(string storageKey, TimeSpan validFor, CancellationToken ct);
    Task DeleteAsync(string storageKey, CancellationToken ct);
}

public sealed record StoredFile(string StorageKey, long SizeBytes, string ContentType);
