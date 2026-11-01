using System.Diagnostics;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// Where the API's telemetry goes is configuration (<c>Telemetry:Exporter</c>), as a deployment sets it.
/// </summary>
public sealed class TelemetryStartupTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void Start_ByDefault_CollectsNothing()
    {
        using WebApplicationFactory<Program> factory = new();

        Assert.Null(factory.Services.GetService<TracerProvider>());
        Assert.Null(factory.Services.GetService<MeterProvider>());
    }

    [Fact]
    public void Start_WithAzureMonitor_TracesAndMeasuresAsTheApi()
    {
        using WebApplicationFactory<Program> factory = WithSettings(
            ("Telemetry:Exporter", "AzureMonitor"),
            ("Telemetry:AzureMonitor:ConnectionString", "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://127.0.0.1:9/"));

        TracerProvider tracing = factory.Services.GetRequiredService<TracerProvider>();

        Assert.NotNull(factory.Services.GetService<MeterProvider>());
        Assert.Equal("hospitalityos-api", tracing.GetResource().Attributes.Single(attribute => attribute.Key == "service.name").Value);

        // Local auth is off on the resource: without the app's identity nothing would arrive.
        AzureMonitorExporterOptions exporter = factory.Services.GetRequiredService<IOptionsMonitor<AzureMonitorExporterOptions>>().Get(Options.DefaultName);
        Assert.NotNull(exporter.Credential);
        Assert.True(exporter.DisableOfflineStorage);
    }

    [Fact]
    public async Task Requests_AreTraced_ButHealthProbesAreNot()
    {
        List<Activity> exported = [];
        using WebApplicationFactory<Program> factory = WithSettings(("Telemetry:Exporter", "Otlp"))
            .WithWebHostBuilder(builder => builder.ConfigureServices(services => services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(exported))));
        using HttpClient client = factory.CreateClient();

        await client.GetAsync(new Uri("/health", UriKind.Relative), Cancellation);
        await client.GetAsync(new Uri("/api/v1/stays", UriKind.Relative), Cancellation);

        // The server's request activity can end just after the client has its response, so wait for it to be exported.
        TracerProvider tracing = factory.Services.GetRequiredService<TracerProvider>();
        for (int attempt = 0; attempt < 50 && !Paths(exported).Contains("/api/v1/stays"); attempt++)
        {
            await Task.Delay(100, Cancellation);
            tracing.ForceFlush();
        }

        Assert.Contains("/api/v1/stays", Paths(exported));
        Assert.DoesNotContain(Paths(exported), path => path.StartsWith("/health", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Zipkin", null, "Telemetry:Exporter 'Zipkin' is unknown")]
    [InlineData("AzureMonitor", null, "Telemetry:AzureMonitor:ConnectionString is required")]
    public void Start_WithAWrongExporter_FailsBeforeServingRequests(string exporter, string? connectionString, string expected)
    {
        using WebApplicationFactory<Program> factory = WithSettings(("Telemetry:Exporter", exporter), ("Telemetry:AzureMonitor:ConnectionString", connectionString ?? ""));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    private static List<string> Paths(List<Activity> exported)
    {
        lock (exported)
        {
            return [.. exported.Select(activity => activity.GetTagItem("url.path") as string).OfType<string>()];
        }
    }

    private static WebApplicationFactory<Program> WithSettings(params (string Key, string Value)[] settings) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            foreach ((string key, string value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });
}
