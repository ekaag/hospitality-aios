using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Organizations.Access;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryStaffAccessRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<StaffAccess, StaffAccessId>(store, unitOfWork, currentTenant), IStaffAccessRepository
{
    public Task<StaffAccess?> FindByStaffMemberAsync(StaffMemberId staffMemberId, CancellationToken cancellationToken) =>
        FindFirstAsync(access => access.StaffMemberId == staffMemberId, cancellationToken);

    public Task<IReadOnlyList<StaffAccess>> ListAsync(CancellationToken cancellationToken) =>
        QueryAsync(access => access, cancellationToken);
}
