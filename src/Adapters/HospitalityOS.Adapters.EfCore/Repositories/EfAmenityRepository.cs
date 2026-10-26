using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Properties.Amenities;
using HospitalityOS.Ports.Tenancy;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfAmenityRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<Amenity, AmenityId>(context, unitOfWork, currentTenant), IAmenityRepository;
