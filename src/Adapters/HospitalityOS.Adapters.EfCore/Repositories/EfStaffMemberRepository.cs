using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Operations.StaffMembers;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfStaffMemberRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<StaffMember, StaffMemberId>(context, unitOfWork, currentTenant), IStaffMemberRepository
{
    public Task<IReadOnlyList<StaffMember>> ListForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken) =>
        QueryAsync(members => members.Where(member => member.PropertyId == propertyId), cancellationToken);
}
