using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Integrations;

/// <summary>
/// Version 1 of <c>IntegrationConfigurationCreated</c>: a property's connector of one kind was configured. Settings are
/// not published.
/// </summary>
/// <param name="IntegrationConfigurationId">The configuration.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="Kind">The connector kind (domain enum name), e.g. <c>Pms</c>.</param>
/// <param name="ProviderName">The provider's registered name, e.g. <c>MockPms</c>.</param>
/// <param name="Status">The status (domain enum name).</param>
public sealed record IntegrationConfigurationCreatedV1(
    Guid IntegrationConfigurationId,
    PropertyId PropertyId,
    string Kind,
    string ProviderName,
    string Status) : IEventPayload;
