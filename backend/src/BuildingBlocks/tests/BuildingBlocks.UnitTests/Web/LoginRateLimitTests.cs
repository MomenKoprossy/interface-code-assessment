using System.Net;
using AwesomeAssertions;
using BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.UnitTests.Web;

/// <summary>The named <see cref="SecurityRegistration.LoginRateLimitPolicy"/> limits attempts per client IP.</summary>
public class LoginRateLimitTests
{
    private static RequestDelegate Pipeline(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddPlatformSecurity(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        var app = new ApplicationBuilder(services.BuildServiceProvider());
        app.UseRateLimiter();
        app.Run(_ => Task.CompletedTask);
        return app.Build();
    }

    private static async Task<int> SendAsync(RequestDelegate pipeline, string ip, bool loginEndpoint = true)
    {
        var context = new DefaultHttpContext { Connection = { RemoteIpAddress = IPAddress.Parse(ip) } };
        var metadata = loginEndpoint
            ? new EndpointMetadataCollection(new EnableRateLimitingAttribute(SecurityRegistration.LoginRateLimitPolicy))
            : EndpointMetadataCollection.Empty;
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, metadata, "test"));
        await pipeline(context);
        return context.Response.StatusCode;
    }

    [Fact]
    public async Task Rejects_attempts_over_the_configured_limit_per_client_ip()
    {
        var pipeline = Pipeline(new() { ["RateLimiting:LoginPermitLimit"] = "2" });

        (await SendAsync(pipeline, "10.0.0.1")).Should().Be(200);
        (await SendAsync(pipeline, "10.0.0.1")).Should().Be(200);
        (await SendAsync(pipeline, "10.0.0.1")).Should().Be(429);
        (await SendAsync(pipeline, "10.0.0.2")).Should().Be(200, "each client IP has its own window");
        (await SendAsync(pipeline, "10.0.0.1", loginEndpoint: false)).Should().Be(200, "other endpoints only use the global limit");
    }

    [Fact]
    public async Task Defaults_to_ten_attempts_per_minute()
    {
        var pipeline = Pipeline([]);

        for (var i = 0; i < 10; i++)
            (await SendAsync(pipeline, "10.0.0.1")).Should().Be(200);
        (await SendAsync(pipeline, "10.0.0.1")).Should().Be(429);
    }
}
