using HospitalityOS.SharedKernel.Domain;

namespace HospitalityOS.Domain.Integrations.IntegrationConfigurations.Events;

/// <summary>
/// A connector configuration changed provider, settings, or status. Settings are left out: consumers read the
/// configuration.
/// </summary>
public sealed record IntegrationConfigurationUpdated(
    IntegrationConfigurationId IntegrationConfigurationId,
    ConnectorKind Kind,
    string ProviderName,
    IntegrationStatus Status,
    DateTimeOffset OccurredAt) : IDomainEvent;
