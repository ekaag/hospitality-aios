using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.PortContractTests.Persistence.Support;

/// <summary>
/// The scope's tenant, set by <see cref="PersistenceSession"/> when it opens, as a host would from claims.
/// </summary>
internal sealed class TestCurrentTenant : ICurrentTenant
{
    public TenantId TenantId { get; set; }
}
