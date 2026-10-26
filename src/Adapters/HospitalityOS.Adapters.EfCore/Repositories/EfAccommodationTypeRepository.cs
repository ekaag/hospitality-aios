using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Properties.AccommodationTypes;
using HospitalityOS.Ports.Tenancy;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfAccommodationTypeRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<AccommodationType, AccommodationTypeId>(context, unitOfWork, currentTenant), IAccommodationTypeRepository;
