using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Activities.PropertyEvents;
using HospitalityOS.Ports.Tenancy;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfPropertyEventRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<PropertyEvent, PropertyEventId>(context, unitOfWork, currentTenant), IPropertyEventRepository;
