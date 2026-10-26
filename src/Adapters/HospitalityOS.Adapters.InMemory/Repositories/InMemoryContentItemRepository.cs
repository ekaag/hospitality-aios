using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Properties.ContentItems;
using HospitalityOS.Ports.Tenancy;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryContentItemRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<ContentItem, ContentItemId>(store, unitOfWork, currentTenant), IContentItemRepository;
