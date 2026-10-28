using HospitalityOS.Ports.Bindings;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HospitalityOS.Bindings.Inference;

/// <summary>
/// Creates and probes (<see cref="IAdapterHealthProbe"/>) every provider each declared task may use, for the port the
/// task needs. Unhealthy if any cannot be created or reports unhealthy; a fallback being healthy does not hide a primary
/// that is not.
/// </summary>
internal sealed class InferenceHealthCheck(InferenceCatalog catalog) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        Dictionary<string, object> data = new(StringComparer.Ordinal);
        List<string> failures = [];
        HashSet<(string Provider, Type Port)> used = [];

        foreach (TaskRoute route in catalog.Configuration.AllRoutes())
        {
            if (catalog.FindTask(route.Task) is not { } task)
            {
                continue;
            }

            foreach (RouteTarget target in route.Targets)
            {
                used.Add((target.Provider.ToUpperInvariant(), task.Port));
            }
        }

        foreach ((string provider, Type port) in used.OrderBy(entry => entry.Provider, StringComparer.Ordinal).ThenBy(entry => entry.Port.Name, StringComparer.Ordinal))
        {
            string key = $"{provider.ToLowerInvariant()}/{port.Name}";
            AdapterHealth health = await ProbeAsync(provider, port, cancellationToken).ConfigureAwait(false);
            data[key] = health.IsHealthy ? "healthy" : health.Description ?? "unhealthy";

            if (!health.IsHealthy)
            {
                failures.Add(key);
            }
        }

        return failures.Count == 0
            ? HealthCheckResult.Healthy("All inference providers healthy.", data)
            : new HealthCheckResult(context.Registration.FailureStatus, $"Unhealthy inference providers: {string.Join(", ", failures)}", data: data);
    }

    private async Task<AdapterHealth> ProbeAsync(string provider, Type port, CancellationToken cancellationToken)
    {
        try
        {
            return catalog.Instance(provider, port) is IAdapterHealthProbe probe
                ? await probe.CheckHealthAsync(cancellationToken).ConfigureAwait(false)
                : AdapterHealth.Healthy;
        }
#pragma warning disable CA1031 // A health check reports any provider failure instead of throwing.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return AdapterHealth.Unhealthy(exception.Message);
        }
    }
}
