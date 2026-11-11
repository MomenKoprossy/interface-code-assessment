using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Web;

/// <summary>Query strings and bodies are left out: they can carry PII (e.g. <c>?search=</c> by email).</summary>
public static class HttpLoggingRegistration
{
    public static IServiceCollection AddPlatformHttpLogging(this IServiceCollection services) =>
        services.AddHttpLogging(options =>
        {
            options.LoggingFields =
                HttpLoggingFields.RequestMethod
                | HttpLoggingFields.RequestPath
                | HttpLoggingFields.ResponseStatusCode
                | HttpLoggingFields.Duration;
            options.CombineLogs = true;
        });
}
