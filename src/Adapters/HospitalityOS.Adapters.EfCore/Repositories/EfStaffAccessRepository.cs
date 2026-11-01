using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Organizations.Access;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfStaffAccessRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<StaffAccess, StaffAccessId>(context, unitOfWork, currentTenant), IStaffAccessRepository
{
    public Task<StaffAccess?> FindByStaffMemberAsync(StaffMemberId staffMemberId, CancellationToken cancellationToken) =>
        FindFirstAsync(access => access.StaffMemberId == staffMemberId, cancellationToken);

    public Task<IReadOnlyList<StaffAccess>> ListAsync(CancellationToken cancellationToken) =>
        QueryAsync(access => access, cancellationToken);
}
