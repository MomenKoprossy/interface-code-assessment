using Microsoft.Extensions.Options;

namespace BuildingBlocks.Storage;

internal sealed class LocalFileStorage(IOptions<FileStorageOptions> opt) : IFileStorage
{
    private readonly LocalStorageOptions _options = opt.Value.Local;

    public async Task<StoredFile> SaveAsync(Stream content, string fileName, string contentType, CancellationToken ct)
    {
        var key = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}/{fileName}";
        var path = ResolvePath(key);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using (var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(file, ct);
        }

        var size = new FileInfo(path).Length;
        return new StoredFile(key, size, contentType);
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        var path = ResolvePath(storageKey);
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task<string> GetDownloadUrlAsync(string storageKey, TimeSpan validFor, CancellationToken ct)
    {
        var baseUrl = _options.PublicBaseUrl;
        var url = string.IsNullOrEmpty(baseUrl)
            ? storageKey
            : $"{baseUrl.TrimEnd('/')}/{storageKey}";
        return Task.FromResult(url);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        var path = ResolvePath(storageKey);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    /// <summary>Rejects keys that resolve outside the storage root (path traversal, e.g. <c>../../etc</c>).</summary>
    private string ResolvePath(string storageKey)
    {
        var root = Path.GetFullPath(_options.RootPath);
        var combined = Path.GetFullPath(Path.Combine(root, storageKey));

        var rootWithSep = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(rootWithSep, StringComparison.Ordinal))
            throw new UnauthorizedAccessException($"Storage key '{storageKey}' resolves outside the storage root.");

        return combined;
    }
}
