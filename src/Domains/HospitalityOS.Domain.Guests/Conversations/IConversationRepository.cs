using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Guests.Conversations;

/// <summary>
/// Persistence port for <see cref="Conversation"/>. Implementations are scoped to the current tenant (P10) and
/// commit through the unit of work.
/// </summary>
public interface IConversationRepository
{
    /// <summary>
    /// Loads a conversation, or returns <c>null</c> when it does not exist in the current tenant.
    /// </summary>
    Task<Conversation?> GetByIdAsync(ConversationId id, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new conversation, persisted when the unit of work commits.
    /// </summary>
    Task AddAsync(Conversation conversation, CancellationToken cancellationToken);
}
