using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry;

namespace HospitalityOS.Adapters.Azure.Telemetry;

/// <summary>
/// Sends the host's OpenTelemetry traces, metrics, and logs to Application Insights (Azure Monitor).
/// </summary>
public static class AzureMonitorTelemetry
{
    /// <summary>
    /// Exports every signal the builder collects to the Application Insights resource <paramref name="connectionString"/>
    /// names, signed in as the app's identity (the resource accepts no key, so the connection string alone sends
    /// nothing). Telemetry that cannot be sent is dropped rather than kept on disk, since containers keep no disk.
    /// </summary>
    public static IOpenTelemetryBuilder UseAzureMonitor(this IOpenTelemetryBuilder builder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return builder.UseAzureMonitorExporter(options =>
        {
            options.ConnectionString = connectionString;
            options.Credential = AzureCredentials.App;
            options.DisableOfflineStorage = true;
        });
    }
}
