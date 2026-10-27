using HospitalityOS.Domain.Integrations.IntegrationConfigurations;
using HospitalityOS.IntegrationRuntime.Providers;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.IntegrationRuntime.Resolution;

internal sealed class IntegrationResolver<TConnector>(
    IIntegrationConfigurationRepository configurations,
    ProviderRegistry<TConnector> registry) : IIntegrationResolver<TConnector>
    where TConnector : class
{
    public async Task<Result<ResolvedConnector<TConnector>>> ResolveAsync(PropertyId propertyId, CancellationToken cancellationToken)
    {
        IntegrationConfiguration? configuration = await configurations.FindForPropertyAsync(propertyId, registry.Kind, cancellationToken).ConfigureAwait(false);

        if (configuration is null)
        {
            return IntegrationRuntimeErrors.NotConfigured(propertyId, registry.Kind);
        }

        if (!configuration.IsActive)
        {
            return IntegrationRuntimeErrors.Disabled(propertyId, registry.Kind);
        }

        TConnector? connector = registry.GetConnector(configuration);

        return connector is null
            ? IntegrationRuntimeErrors.ProviderUnavailable(registry.Kind, configuration.ProviderName)
            : new ResolvedConnector<TConnector>(connector, configuration.ProviderName);
    }
}
