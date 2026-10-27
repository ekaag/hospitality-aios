using HospitalityOS.Domain.Integrations.IntegrationConfigurations;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.IntegrationRuntime;

/// <summary>
/// Expected failures of resolving, configuring, and linking integrations.
/// </summary>
public static class IntegrationRuntimeErrors
{
    /// <summary>
    /// Code of <see cref="NotConfigured"/>.
    /// </summary>
    public const string NotConfiguredCode = "integration.not_configured";

    /// <summary>
    /// Code of <see cref="Disabled"/>.
    /// </summary>
    public const string DisabledCode = "integration.disabled";

    /// <summary>
    /// Code of <see cref="ProviderUnavailable"/>.
    /// </summary>
    public const string ProviderUnavailableCode = "integration.provider_unavailable";

    /// <summary>
    /// Code of the validation error for a provider name no registered provider has.
    /// </summary>
    public const string ProviderUnknownCode = "integration.provider_unknown";

    /// <summary>
    /// Code of the validation errors for settings the provider rejects.
    /// </summary>
    public const string SettingsInvalidCode = "integration.settings_invalid";

    /// <summary>
    /// Code of <see cref="ReferenceConflict"/>.
    /// </summary>
    public const string ReferenceConflictCode = "integration.reference_conflict";

    /// <summary>
    /// The property does not exist in the current tenant.
    /// </summary>
    public static Error PropertyNotFound { get; } = Error.NotFound("property.not_found", "There is no such property.");

    /// <summary>
    /// The property has no configuration for this kind of connector.
    /// </summary>
    public static Error NotConfigured(PropertyId propertyId, ConnectorKind kind) =>
        Error.NotFound(NotConfiguredCode, $"Property {propertyId} has no {kind} integration configured.");

    /// <summary>
    /// The property's connector of this kind is switched off.
    /// </summary>
    public static Error Disabled(PropertyId propertyId, ConnectorKind kind) =>
        Error.InvalidState(DisabledCode, $"Property {propertyId}'s {kind} integration is disabled.");

    /// <summary>
    /// The configured provider is not registered in this deployment, e.g. after it was removed.
    /// </summary>
    public static Error ProviderUnavailable(ConnectorKind kind, string providerName) =>
        Error.InvalidState(ProviderUnavailableCode, $"{kind} provider '{providerName}' is not registered in this deployment.");

    /// <summary>
    /// The provider ID is already linked to another canonical record.
    /// </summary>
    public static Error ReferenceConflict(string providerName, string entityType, string externalId) =>
        Error.Conflict(ReferenceConflictCode, $"{providerName} {entityType} '{externalId}' is already linked to another record.");
}
