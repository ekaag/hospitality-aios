using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Ports.Tenancy;

/// <summary>
/// The scope's tenant, set once by whatever starts the scope: authentication from the caller's claims (F1), or the
/// event processor from the envelope it is handling. Reading it before it is set fails loudly rather than reading
/// another tenant's data (P10). Hosts register it scoped and expose it as <see cref="ICurrentTenant"/>.
/// </summary>
public sealed class TenantContext : ICurrentTenant
{
    private TenantId? tenantId;

    /// <inheritdoc />
    public TenantId TenantId => tenantId ?? throw new InvalidOperationException("No tenant is set for this scope; it is set from the caller's claims or the event being handled.");

    /// <summary>
    /// Whether the tenant has been set.
    /// </summary>
    public bool IsSet => tenantId is not null;

    /// <summary>
    /// Sets the tenant for the rest of the scope. Setting the same tenant again is a no-op; switching tenants within a
    /// scope is a programming error.
    /// </summary>
    /// <param name="tenant">The tenant.</param>
    public void Set(TenantId tenant)
    {
        if (tenantId is { } current && current != tenant)
        {
            throw new InvalidOperationException($"This scope already acts for tenant {current}; it cannot switch to {tenant}.");
        }

        tenantId = tenant;
    }
}
