using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Guests.GuestMessages;

/// <summary>
/// Persistence port for <see cref="GuestMessage"/>. Implementations are scoped to the current tenant (P10) and
/// commit through the unit of work.
/// </summary>
public interface IGuestMessageRepository
{
    /// <summary>
    /// Loads a message, or returns <c>null</c> when it does not exist in the current tenant.
    /// </summary>
    Task<GuestMessage?> GetByIdAsync(GuestMessageId id, CancellationToken cancellationToken);

    /// <summary>
    /// Finds a message by the channel's message ID within a conversation, or returns <c>null</c>; used to ignore
    /// redelivered messages (P8). Backed by a unique index in persistence.
    /// </summary>
    Task<GuestMessage?> FindByClientMessageIdAsync(ConversationId conversationId, string clientMessageId, CancellationToken cancellationToken);

    /// <summary>
    /// The most recent messages of a conversation, oldest first, for concierge context.
    /// </summary>
    Task<IReadOnlyList<GuestMessage>> ListRecentAsync(ConversationId conversationId, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new message, persisted when the unit of work commits.
    /// </summary>
    Task AddAsync(GuestMessage message, CancellationToken cancellationToken);
}
