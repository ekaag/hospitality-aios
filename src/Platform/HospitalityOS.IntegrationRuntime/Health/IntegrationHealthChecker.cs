using HospitalityOS.Domain.Integrations.IntegrationConfigurations;
using HospitalityOS.IntegrationRuntime.Providers;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.IntegrationRuntime.Health;

internal sealed class IntegrationHealthChecker(
    IIntegrationConfigurationRepository configurations,
    IEnumerable<IProviderRegistry> registries) : IIntegrationHealth
{
    private static readonly AdapterHealth notChecked = new(true, "Disabled; not checked.");

    public async Task<IReadOnlyList<IntegrationHealthReport>> CheckAsync(PropertyId propertyId, CancellationToken cancellationToken)
    {
        IReadOnlyList<IntegrationConfiguration> configured = await configurations.ListForPropertyAsync(propertyId, cancellationToken).ConfigureAwait(false);
        List<IntegrationHealthReport> reports = [];

        foreach (IntegrationConfiguration configuration in configured.OrderBy(configuration => configuration.Kind))
        {
            AdapterHealth health = configuration.IsActive
                ? await registries.Single(registry => registry.Kind == configuration.Kind).CheckHealthAsync(configuration, cancellationToken).ConfigureAwait(false)
                : notChecked;

            reports.Add(new IntegrationHealthReport(configuration.Kind, configuration.ProviderName, configuration.Status, health));
        }

        return reports;
    }
}
