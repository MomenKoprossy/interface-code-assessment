namespace BuildingBlocks.Storage;

/// <summary>
/// Abstraction over physical file storage. Implemented by <see cref="LocalFileStorage"/> (disk)
/// and <see cref="S3FileStorage"/> (AWS S3 / MinIO / DO Spaces), selected by configuration.
/// </summary>
public interface IFileStorage
{
    Task<StoredFile> SaveAsync(Stream content, string fileName, string contentType, CancellationToken ct);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct);
    Task<string> GetDownloadUrlAsync(string storageKey, TimeSpan validFor, CancellationToken ct);
    Task DeleteAsync(string storageKey, CancellationToken ct);
}

/// <summary>Result of persisting a file: the provider-specific locator plus basic metadata.</summary>
public sealed record StoredFile(string StorageKey, long SizeBytes, string ContentType);
