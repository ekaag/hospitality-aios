using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Guests.Guests;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfGuestRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<Guest, GuestId>(context, unitOfWork, currentTenant), IGuestRepository;
