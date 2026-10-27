using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Integrations.IntegrationConfigurations.Events;

/// <summary>
/// A property's connector of one kind was configured. Settings are left out: consumers read the configuration.
/// </summary>
public sealed record IntegrationConfigurationCreated(
    IntegrationConfigurationId IntegrationConfigurationId,
    PropertyId PropertyId,
    ConnectorKind Kind,
    string ProviderName,
    IntegrationStatus Status,
    DateTimeOffset OccurredAt) : IDomainEvent;
