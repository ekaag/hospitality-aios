using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Operations.StaffMembers;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryStaffMemberRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<StaffMember, StaffMemberId>(store, unitOfWork, currentTenant), IStaffMemberRepository
{
    public Task<IReadOnlyList<StaffMember>> ListForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken) =>
        QueryAsync(members => members.Where(member => member.PropertyId == propertyId), cancellationToken);
}
