using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Guests.Guests;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryGuestRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<Guest, GuestId>(store, unitOfWork, currentTenant), IGuestRepository;
