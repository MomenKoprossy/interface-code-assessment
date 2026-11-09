using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Web;

/// <summary>
/// Inbound request/response logging for every request — metadata only (method, path, query, status,
/// duration), one entry per request. Bodies are off by default (PII + size) (§15.5).
/// </summary>
public static class HttpLoggingRegistration
{
    public static IServiceCollection AddPlatformHttpLogging(this IServiceCollection services) =>
        services.AddHttpLogging(options =>
        {
            options.LoggingFields =
                HttpLoggingFields.RequestMethod
                | HttpLoggingFields.RequestPath
                | HttpLoggingFields.RequestQuery
                | HttpLoggingFields.ResponseStatusCode
                | HttpLoggingFields.Duration;
            options.CombineLogs = true;
        });
}
