using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Ports.Tenancy;

/// <summary>
/// The tenant the current request or message acts for. Persistence adapters filter every read and check every
/// write against it, so isolation holds at the data-access layer rather than by convention (P10). Scoped: hosts
/// set it from the caller's claims or the message envelope.
/// </summary>
public interface ICurrentTenant
{
    /// <summary>
    /// The tenant.
    /// </summary>
    TenantId TenantId { get; }
}
