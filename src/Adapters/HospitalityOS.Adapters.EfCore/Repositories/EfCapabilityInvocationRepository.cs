using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Capabilities.CapabilityInvocations;
using HospitalityOS.Ports.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfCapabilityInvocationRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<CapabilityInvocation, CapabilityInvocationId>(context, unitOfWork, currentTenant), ICapabilityInvocationRepository
{
    public Task<CapabilityInvocation?> FindSucceededAsync(string capabilityName, string idempotencyKey, CancellationToken cancellationToken)
    {
        string key = KeyColumns.SucceededIdempotency(capabilityName, idempotencyKey);

        return FindFirstAsync(
            invocation => EF.Property<string?>(invocation, KeyColumns.SucceededIdempotencyKey) == key,
            cancellationToken);
    }
}
