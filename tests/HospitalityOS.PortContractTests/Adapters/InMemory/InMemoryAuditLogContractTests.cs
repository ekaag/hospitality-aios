using HospitalityOS.Adapters.InMemory;
using HospitalityOS.PortContractTests.Auditing;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalityOS.PortContractTests.Adapters.InMemory;

public sealed class InMemoryAuditLogContractTests : AuditLogContractTests
{
    protected override void AddPersistence(IServiceCollection services) => services.AddInMemoryPersistence();
}
