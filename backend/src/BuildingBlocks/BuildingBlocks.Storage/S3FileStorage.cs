using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Storage;

/// <summary>
/// S3-compatible <see cref="IFileStorage"/> (AWS S3, MinIO, DigitalOcean Spaces — only
/// <c>ServiceUrl</c>/<c>ForcePathStyle</c> differ). Uses standard SigV4 payload signing on upload
/// (works over both http MinIO dev endpoints and https S3) and presigned GET URLs for download.
/// Note: AWSSDK v4 forbids <c>DisablePayloadSigning=true</c> over plain http, so it is NOT set.
/// </summary>
internal sealed class S3FileStorage(IAmazonS3 s3, IOptions<FileStorageOptions> opt) : IFileStorage
{
    private readonly string _bucket = opt.Value.S3.Bucket;

    public async Task<StoredFile> SaveAsync(Stream content, string fileName, string contentType, CancellationToken ct)
    {
        var key = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}/{fileName}";
        // Capture the size BEFORE the upload: AWSSDK v4 disposes the input stream after PutObject,
        // so reading content.Length afterwards throws ObjectDisposedException.
        var size = content.CanSeek ? content.Length : 0L;
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType
        }, ct);
        return new StoredFile(key, size, contentType);
    }

    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        var response = await s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _bucket,
            Key = storageKey
        }, ct);
        return response.ResponseStream;
    }

    public Task<string> GetDownloadUrlAsync(string storageKey, TimeSpan validFor, CancellationToken ct) =>
        s3.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = storageKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(validFor)
        });

    public Task DeleteAsync(string storageKey, CancellationToken ct) =>
        s3.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _bucket,
            Key = storageKey
        }, ct);
}
