using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Guests.Conversations;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryConversationRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<Conversation, ConversationId>(store, unitOfWork, currentTenant), IConversationRepository;
