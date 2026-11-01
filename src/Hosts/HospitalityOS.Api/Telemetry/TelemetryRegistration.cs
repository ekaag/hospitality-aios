using HospitalityOS.Adapters.Azure.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace HospitalityOS.Api.Telemetry;

/// <summary>
/// The API's telemetry through OpenTelemetry (K6b): traces of requests and outgoing calls, request, client, and runtime
/// metrics, and logs, plus the platform's own sources and meters (<c>HospitalityOS.*</c>, which L1 adds). Where it goes is
/// configuration (<c>Telemetry:Exporter</c>), like any binding: <c>None</c> (the default, nothing collected),
/// <c>Otlp</c> (the standard <c>OTEL_EXPORTER_OTLP_*</c> variables, e.g. a local dashboard), or <c>AzureMonitor</c>
/// (<c>Telemetry:AzureMonitor:ConnectionString</c>). Health probes are not traced. Query strings are redacted by the
/// instrumentation, so tokens passed in them (SignalR's) never leave the process. A wrong setting stops the host at
/// startup.
/// </summary>
internal static class TelemetryRegistration
{
    public const string SectionName = "Telemetry";
    public const string ServiceName = "hospitalityos-api";

    /// <summary>
    /// The platform's own activity sources and meters.
    /// </summary>
    public const string PlatformSources = "HospitalityOS.*";

    public static IServiceCollection AddConfiguredTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection(SectionName);
        string exporter = section["Exporter"] ?? "None";

        if (string.Equals(exporter, "None", StringComparison.OrdinalIgnoreCase))
        {
            return services;
        }

        IOpenTelemetryBuilder telemetry = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName, serviceVersion: typeof(TelemetryRegistration).Assembly.GetName().Version?.ToString()))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options => options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddSource(PlatformSources))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(PlatformSources))
            .WithLogging();

        switch (exporter.ToUpperInvariant())
        {
            case "OTLP":
                telemetry.UseOtlpExporter();
                break;

            case "AZUREMONITOR":
                telemetry.UseAzureMonitor(section["AzureMonitor:ConnectionString"] is { Length: > 0 } connectionString
                    ? connectionString
                    : throw new InvalidOperationException($"{SectionName}:AzureMonitor:ConnectionString is required for the AzureMonitor exporter."));
                break;

            default:
                throw new InvalidOperationException($"{SectionName}:Exporter '{exporter}' is unknown; use None, Otlp, or AzureMonitor.");
        }

        return services;
    }
}
