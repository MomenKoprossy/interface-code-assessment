using System.Globalization;
using System.Net;
using AwesomeAssertions;
using BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.UnitTests.Web;

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

    private static async Task<int> SendAsync(RequestDelegate pipeline, string ip, bool loginEndpoint = true) =>
        (await SendForResponseAsync(pipeline, ip, loginEndpoint)).StatusCode;

    private static async Task<HttpResponse> SendForResponseAsync(RequestDelegate pipeline, string ip, bool loginEndpoint = true)
    {
        var context = new DefaultHttpContext { Connection = { RemoteIpAddress = IPAddress.Parse(ip) } };
        context.Response.Body = new MemoryStream();
        var metadata = loginEndpoint
            ? new EndpointMetadataCollection(new EnableRateLimitingAttribute(SecurityRegistration.LoginRateLimitPolicy))
            : EndpointMetadataCollection.Empty;
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, metadata, "test"));
        await pipeline(context);
        return context.Response;
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

    [Fact]
    public async Task Rejection_sets_Retry_After_and_leaves_the_body_to_status_code_pages()
    {
        var pipeline = Pipeline(new() { ["RateLimiting:LoginPermitLimit"] = "1" });
        await SendAsync(pipeline, "10.0.0.1");

        var rejected = await SendForResponseAsync(pipeline, "10.0.0.1");

        rejected.StatusCode.Should().Be(429);
        int.Parse(rejected.Headers.RetryAfter.ToString(), CultureInfo.InvariantCulture).Should().BeInRange(1, 60);
        rejected.Body.Length.Should().Be(0, "OnRejected must not write a body; UseStatusCodePages renders the ProblemDetails");
    }
}
