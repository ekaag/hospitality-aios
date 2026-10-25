namespace HospitalityOS.SharedKernel.Tenancy;

/// <summary>
/// Marks an aggregate as owned by a single tenant, so persistence can enforce isolation at the
/// data-access layer rather than by convention (P10).
/// </summary>
public interface ITenantScoped
{
    /// <summary>
    /// The owning tenant.
    /// </summary>
    TenantId TenantId { get; }
}
