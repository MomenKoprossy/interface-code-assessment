using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Storage;

public static class StorageRegistration
{
    public static IServiceCollection AddFileStorage(this IServiceCollection s, IConfiguration cfg)
    {
        // Bind via the referenced Configuration.Binder (the IConfiguration-taking Configure overload
        // lives in Microsoft.Extensions.Options.ConfigurationExtensions, which is not referenced here).
        s.Configure<FileStorageOptions>(o => cfg.GetSection("Storage").Bind(o));

        var provider = cfg["Storage:Provider"] ?? "Local";
        if (provider.Equals("S3", StringComparison.OrdinalIgnoreCase))
        {
            s.AddSingleton<IAmazonS3>(sp =>
            {
                var o = sp.GetRequiredService<IOptions<FileStorageOptions>>().Value.S3;
                return new AmazonS3Client(
                    new BasicAWSCredentials(o.AccessKey, o.SecretKey),
                    new AmazonS3Config
                    {
                        ServiceURL = o.ServiceUrl,
                        ForcePathStyle = o.ForcePathStyle,
                        // AWSSDK v4 adds CRC checksum trailers by default ("WHEN_SUPPORTED"), which
                        // S3-compatible endpoints like MinIO / DO Spaces reject (x-amz-content-sha256
                        // mismatch). WHEN_REQUIRED only adds a checksum when the operation mandates it.
                        RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                        ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
                    });
            });
            s.AddScoped<IFileStorage, S3FileStorage>();
        }
        else
        {
            s.AddScoped<IFileStorage, LocalFileStorage>();
        }

        return s;
    }
}
