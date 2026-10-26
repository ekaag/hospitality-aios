using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Organizations.Organizations;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfOrganizationRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<Organization, OrganizationId>(context, unitOfWork, currentTenant), IOrganizationRepository;
