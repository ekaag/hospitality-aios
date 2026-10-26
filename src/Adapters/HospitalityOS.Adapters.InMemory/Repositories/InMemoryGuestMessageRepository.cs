using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Guests.GuestMessages;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryGuestMessageRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<GuestMessage, GuestMessageId>(store, unitOfWork, currentTenant), IGuestMessageRepository
{
    public Task<GuestMessage?> FindByClientMessageIdAsync(ConversationId conversationId, string clientMessageId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clientMessageId);

        string trimmed = clientMessageId.Trim();

        return FindFirstAsync(
            message => message.ConversationId == conversationId && string.Equals(message.ClientMessageId, trimmed, StringComparison.Ordinal),
            cancellationToken);
    }

    public Task<IReadOnlyList<GuestMessage>> ListRecentAsync(ConversationId conversationId, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        return QueryAsync(
            messages => messages
                .Where(message => message.ConversationId == conversationId)
                .OrderByDescending(message => message.SentAt)
                .ThenByDescending(message => message.Id.Value)
                .Take(limit)
                .Reverse(),
            cancellationToken);
    }
}
