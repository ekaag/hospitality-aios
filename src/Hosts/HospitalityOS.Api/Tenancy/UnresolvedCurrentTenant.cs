using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Api.Tenancy;

/// <summary>
/// Placeholder until authentication (F1) resolves the tenant from the caller's claims. Nothing reaches persistence
/// before then; if something does, it fails loudly instead of reading another tenant's data (P10).
/// </summary>
internal sealed class UnresolvedCurrentTenant : ICurrentTenant
{
    public TenantId TenantId => throw new InvalidOperationException("No tenant is resolved for this request; tenant resolution arrives with authentication (F1).");
}
