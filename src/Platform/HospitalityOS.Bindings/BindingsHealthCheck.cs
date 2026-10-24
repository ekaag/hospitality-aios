using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HospitalityOS.Bindings;

/// <summary>
/// Resolves every configured binding of every registered port and runs adapter health probes.
/// Unhealthy if any binding cannot be resolved or reports unhealthy.
/// </summary>
internal sealed class BindingsHealthCheck : IHealthCheck
{
    private readonly IServiceProvider services;
    private readonly IEnumerable<PortRegistration> registrations;

    public BindingsHealthCheck(IServiceProvider services, IEnumerable<PortRegistration> registrations)
    {
        this.services = services;
        this.registrations = registrations;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        Dictionary<string, object> data = new(StringComparer.Ordinal);
        List<string> failures = [];

        foreach (PortRegistration registration in registrations)
        {
            foreach (BindingHealth binding in await registration.CheckHealthAsync(services, cancellationToken).ConfigureAwait(false))
            {
                data[binding.Path] = binding.Health.IsHealthy
                    ? $"{binding.Adapter}: healthy"
                    : $"{binding.Adapter}: {binding.Health.Description}";

                if (!binding.Health.IsHealthy)
                {
                    failures.Add(binding.Path);
                }
            }
        }

        return failures.Count == 0
            ? HealthCheckResult.Healthy("All bindings healthy.", data)
            : new HealthCheckResult(context.Registration.FailureStatus, $"Unhealthy bindings: {string.Join(", ", failures)}", data: data);
    }
}
