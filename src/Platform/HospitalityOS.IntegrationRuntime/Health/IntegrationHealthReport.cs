using HospitalityOS.Domain.Integrations.IntegrationConfigurations;
using HospitalityOS.Ports.Bindings;

namespace HospitalityOS.IntegrationRuntime.Health;

/// <summary>
/// The health of one of a property's connectors.
/// </summary>
/// <param name="Kind">Which connector.</param>
/// <param name="ProviderName">The configured provider.</param>
/// <param name="Status">Whether the connector is enabled; a disabled one is reported healthy and not checked.</param>
/// <param name="Health">The probe's result.</param>
public sealed record IntegrationHealthReport(ConnectorKind Kind, string ProviderName, IntegrationStatus Status, AdapterHealth Health);
