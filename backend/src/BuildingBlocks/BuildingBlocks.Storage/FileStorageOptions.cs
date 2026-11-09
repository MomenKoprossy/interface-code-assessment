namespace BuildingBlocks.Storage;

/// <summary>
/// Bound from configuration section <c>Storage</c>. <c>Storage:Provider</c> selects the
/// implementation: <c>"Local"</c> (disk) or <c>"S3"</c> (S3-compatible).
/// </summary>
public sealed class FileStorageOptions
{
    public string Provider { get; set; } = "Local";
    public LocalStorageOptions Local { get; set; } = new();
    public S3StorageOptions S3 { get; set; } = new();
}

public sealed class LocalStorageOptions
{
    public string RootPath { get; set; } = "App_Data/files";
    public string PublicBaseUrl { get; set; } = "";
}

public sealed class S3StorageOptions
{
    public string ServiceUrl { get; set; } = "";
    public string Bucket { get; set; } = "";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public bool ForcePathStyle { get; set; } = true; // true for MinIO / DO Spaces
}
