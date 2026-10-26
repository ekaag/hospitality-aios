using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Properties.Policies;
using HospitalityOS.Ports.Tenancy;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfPolicyRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<Policy, PolicyId>(context, unitOfWork, currentTenant), IPolicyRepository;
