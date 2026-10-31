using Hangfire;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace BigFatFile.Server.Observability;

internal static class TelemetryServices
{
    public const string ServiceName = "bff-origin";

    public static void AddTelemetry(this WebApplicationBuilder builder, string serverVersion)
    {
        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName, serviceVersion: serverVersion))
            .WithTracing(tracing => tracing
                .SetSampler(builder.Environment.IsDevelopment()
                    ? new AlwaysOnSampler()
                    : new ParentBasedSampler(new TraceIdRatioBasedSampler(0.1)))
                .AddAspNetCoreInstrumentation(options => options.Filter = context => !IsProbe(context.Request.Path))
                .AddHttpClientInstrumentation()
                .AddNpgsql()
                .AddHangfireInstrumentation())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("Npgsql")
                .AddPrometheusExporter())
            .WithLogging(_ => { }, options =>
            {
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
            });

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            telemetry.UseOtlpExporter();
        }
    }

    public static void MapMetrics(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPrometheusScrapingEndpoint("/metrics");

    private static bool IsProbe(PathString path) =>
        path.StartsWithSegments("/health") || path.StartsWithSegments("/metrics");
}
