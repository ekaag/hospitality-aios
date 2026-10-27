using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.IntegrationContracts.Providers;

/// <summary>
/// What a provider needs to create the connector serving one property.
/// </summary>
/// <param name="TenantId">The property's tenant.</param>
/// <param name="PropertyId">The property served.</param>
/// <param name="Settings">The property's non-secret settings for this provider, by name, ignoring case; secrets are
/// named here and read from the secret store (P10).</param>
public sealed record ConnectorContext(TenantId TenantId, PropertyId PropertyId, IReadOnlyDictionary<string, string> Settings);
