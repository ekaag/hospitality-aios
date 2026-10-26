using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Guests.Conversations;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfConversationRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<Conversation, ConversationId>(context, unitOfWork, currentTenant), IConversationRepository;
