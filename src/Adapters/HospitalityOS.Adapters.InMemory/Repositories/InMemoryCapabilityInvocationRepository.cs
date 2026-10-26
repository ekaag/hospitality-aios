using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Capabilities.CapabilityInvocations;
using HospitalityOS.Ports.Tenancy;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryCapabilityInvocationRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<CapabilityInvocation, CapabilityInvocationId>(store, unitOfWork, currentTenant), ICapabilityInvocationRepository
{
    public Task<CapabilityInvocation?> FindSucceededAsync(string capabilityName, string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capabilityName);
        ArgumentNullException.ThrowIfNull(idempotencyKey);

        return FindFirstAsync(
            invocation => invocation.Status == CapabilityInvocationStatus.Succeeded
                && string.Equals(invocation.CapabilityName, capabilityName, StringComparison.Ordinal)
                && string.Equals(invocation.IdempotencyKey, idempotencyKey, StringComparison.Ordinal),
            cancellationToken);
    }
}
