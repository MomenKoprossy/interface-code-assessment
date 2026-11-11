using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Call <em>after</em> <see cref="Extensions.AddServiceDefaults{TBuilder}"/>. Adds EF Core tracing (SQL on
/// <c>db.query.text</c>) + EF metrics. The template's gRPC/Redis/Rebus/YARP instrumentation is intentionally
/// not wired: this app doesn't use those (see .claude/docs/scalability.md).
/// </summary>
public static class PlatformObservability
{
    public static TBuilder AddPlatformObservability<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddEntityFrameworkCoreInstrumentation())
            .WithMetrics(metrics => metrics.AddMeter("Microsoft.EntityFrameworkCore"));

        return builder;
    }
}
