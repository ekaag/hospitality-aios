using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Guests.GuestMessages;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfGuestMessageRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<GuestMessage, GuestMessageId>(context, unitOfWork, currentTenant), IGuestMessageRepository
{
    public Task<GuestMessage?> FindByClientMessageIdAsync(ConversationId conversationId, string clientMessageId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clientMessageId);

        string trimmed = clientMessageId.Trim();

        return FindFirstAsync(
            message => message.ConversationId == conversationId && message.ClientMessageId == trimmed,
            cancellationToken);
    }

    public async Task<IReadOnlyList<GuestMessage>> ListRecentAsync(ConversationId conversationId, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        IReadOnlyList<GuestMessage> newestFirst = await QueryAsync(
            messages => messages
                .Where(message => message.ConversationId == conversationId)
                .OrderByDescending(message => message.SentAt)
                .ThenByDescending(message => message.Id)
                .Take(limit),
            cancellationToken).ConfigureAwait(false);

        return [.. newestFirst.Reverse()];
    }
}
