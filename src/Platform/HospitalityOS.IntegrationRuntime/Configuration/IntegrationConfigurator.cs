using HospitalityOS.Domain.Integrations.IntegrationConfigurations;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.IntegrationRuntime.Providers;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.IntegrationRuntime.Configuration;

internal sealed class IntegrationConfigurator(
    IPropertyRepository properties,
    IIntegrationConfigurationRepository configurations,
    IEnumerable<IProviderRegistry> registries,
    IBindingResolver<IIdGenerator> idGenerators,
    TimeProvider timeProvider) : IIntegrationConfigurator
{
    public IReadOnlyList<string> ListProviders(ConnectorKind kind) => FindRegistry(kind)?.ProviderNames ?? [];

    public async Task<Result<IntegrationConfigurationId>> ConfigureAsync(
        PropertyId propertyId,
        ConnectorKind kind,
        string providerName,
        IReadOnlyDictionary<string, string> settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(providerName);
        ArgumentNullException.ThrowIfNull(settings);

        IProviderRegistry? registry = FindRegistry(kind);

        if (registry is null)
        {
            return Error.Validation("kind", "integration.kind_invalid", $"'{kind}' is not a known connector kind.");
        }

        Property? property = await properties.GetByIdAsync(propertyId, cancellationToken).ConfigureAwait(false);

        if (property is null)
        {
            return IntegrationRuntimeErrors.PropertyNotFound;
        }

        string? provider = registry.FindProviderName(providerName);

        if (provider is null)
        {
            string registered = registry.ProviderNames.Count == 0 ? "(none)" : string.Join(", ", registry.ProviderNames);

            return Error.Validation(
                "providerName",
                IntegrationRuntimeErrors.ProviderUnknownCode,
                $"No {kind} provider named '{providerName}' is registered. Registered: {registered}.");
        }

        IReadOnlyList<string> problems = registry.ValidateSettings(provider, settings);

        if (problems.Count > 0)
        {
            return ValidationError.From([.. problems.Select(problem => new FieldError("settings", IntegrationRuntimeErrors.SettingsInvalidCode, problem))]);
        }

        IntegrationSetting[] values = [.. settings.Select(setting => new IntegrationSetting(setting.Key, setting.Value))];
        DateTimeOffset now = timeProvider.GetUtcNow();
        IntegrationConfiguration? existing = await configurations.FindForPropertyAsync(propertyId, kind, cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            Result reconfigured = existing.Reconfigure(provider, values, now);

            return reconfigured.IsFailure ? reconfigured.Error : existing.Id;
        }

        Result<IntegrationConfiguration> created = IntegrationConfiguration.Create(
            new IntegrationConfigurationId(idGenerators.Resolve().NewId()),
            property.TenantId,
            propertyId,
            kind,
            provider,
            values,
            now);

        if (created.IsFailure)
        {
            return created.Error;
        }

        await configurations.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);

        return created.Value.Id;
    }

    public async Task<Result> SetEnabledAsync(PropertyId propertyId, ConnectorKind kind, bool enabled, CancellationToken cancellationToken)
    {
        IntegrationConfiguration? configuration = await configurations.FindForPropertyAsync(propertyId, kind, cancellationToken).ConfigureAwait(false);

        if (configuration is null)
        {
            return IntegrationRuntimeErrors.NotConfigured(propertyId, kind);
        }

        if (enabled)
        {
            configuration.Enable(timeProvider.GetUtcNow());
        }
        else
        {
            configuration.Disable(timeProvider.GetUtcNow());
        }

        return Result.Success();
    }

    private IProviderRegistry? FindRegistry(ConnectorKind kind) => registries.FirstOrDefault(registry => registry.Kind == kind);
}
