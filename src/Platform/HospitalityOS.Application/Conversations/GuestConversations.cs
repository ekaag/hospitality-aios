using HospitalityOS.Application.Administration;
using HospitalityOS.Contracts;
using HospitalityOS.Contracts.Api.Conversations;
using HospitalityOS.Domain.Guests.Conversations;
using HospitalityOS.Domain.Guests.GuestMessages;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using ContractSender = HospitalityOS.Contracts.Api.Conversations.MessageSender;
using ContractStatus = HospitalityOS.Contracts.Api.Conversations.ConversationStatus;
using DomainSender = HospitalityOS.Domain.Guests.GuestMessages.MessageSender;

namespace HospitalityOS.Application.Conversations;

internal sealed class GuestConversations(
    IConversationRepository conversations,
    IGuestMessageRepository messages,
    IStayContextRepository stays,
    IUnitOfWork unitOfWork,
    ICurrentTenant currentTenant,
    RecordIds ids,
    TimeProvider timeProvider) : IGuestConversations
{
    /// <summary>
    /// Messages returned by <see cref="ListMessagesAsync"/>.
    /// </summary>
    public const int RecentMessages = 50;

    public async Task<Result<ConversationResponse>> StartAsync(GuestCaller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        StayContext? stay = await ActiveStayAsync(caller, cancellationToken).ConfigureAwait(false);

        if (stay is null)
        {
            return ConversationErrors.StayNotActive;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Conversation conversation = Conversation.Start(new ConversationId(ids.Next()), currentTenant.TenantId, caller.PropertyId, ConversationChannel.Web, now).Value;
        Result linked = conversation.LinkStay(stay, now);

        if (linked.IsFailure)
        {
            return linked.Error;
        }

        await conversations.AddAsync(conversation, cancellationToken).ConfigureAwait(false);
        Result<CommitReceipt> commit = await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return commit.IsFailure ? commit.Error : Response(conversation);
    }

    public async Task<Result<ConversationResponse>> GetAsync(GuestCaller caller, Guid conversationId, CancellationToken cancellationToken)
    {
        Result<Conversation> conversation = await OwnAsync(caller, conversationId, cancellationToken).ConfigureAwait(false);

        return conversation.IsFailure ? conversation.Error : Response(conversation.Value);
    }

    public async Task<Result<PostedMessage>> PostMessageAsync(GuestCaller caller, Guid conversationId, PostMessageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<Conversation> conversation = await OwnAsync(caller, conversationId, cancellationToken).ConfigureAwait(false);

        if (conversation.IsFailure)
        {
            return conversation.Error;
        }

        if (!string.IsNullOrWhiteSpace(request.ClientMessageId))
        {
            GuestMessage? recorded = await messages.FindByClientMessageIdAsync(conversation.Value.Id, request.ClientMessageId.Trim(), cancellationToken).ConfigureAwait(false);

            if (recorded is not null)
            {
                return new PostedMessage(Response(recorded), Created: false);
            }
        }

        Result<GuestMessage> message = GuestMessage.Record(
            new GuestMessageId(ids.Next()), conversation.Value, DomainSender.Guest, request.Text ?? "", request.ClientMessageId, timeProvider.GetUtcNow());

        if (message.IsFailure)
        {
            return message.Error;
        }

        await messages.AddAsync(message.Value, cancellationToken).ConfigureAwait(false);
        Result<CommitReceipt> commit = await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return commit.IsFailure ? commit.Error : new PostedMessage(Response(message.Value), Created: true);
    }

    public async Task<Result<IReadOnlyList<GuestMessageResponse>>> ListMessagesAsync(GuestCaller caller, Guid conversationId, CancellationToken cancellationToken)
    {
        Result<Conversation> conversation = await OwnAsync(caller, conversationId, cancellationToken).ConfigureAwait(false);

        if (conversation.IsFailure)
        {
            return conversation.Error;
        }

        IReadOnlyList<GuestMessage> recent = await messages.ListRecentAsync(conversation.Value.Id, RecentMessages, cancellationToken).ConfigureAwait(false);

        return Result.Success<IReadOnlyList<GuestMessageResponse>>([.. recent.Select(Response)]);
    }

    private static ConversationResponse Response(Conversation conversation) =>
        new(conversation.Id, conversation.PropertyId, conversation.StayContextId, EnumMirror.To<ContractStatus>(conversation.Status), conversation.StartedAt);

    private static GuestMessageResponse Response(GuestMessage message) =>
        new(message.Id, message.ConversationId, EnumMirror.To<ContractSender>(message.Sender), message.Text, message.ClientMessageId, message.SentAt);

    /// <summary>
    /// The caller's stay, if it exists, is theirs at the token's property, and is reserved or in house.
    /// </summary>
    private async Task<StayContext?> ActiveStayAsync(GuestCaller caller, CancellationToken cancellationToken)
    {
        StayContext? stay = await stays.GetByIdAsync(caller.StayContextId, cancellationToken).ConfigureAwait(false);

        return stay is not null && stay.GuestId == caller.GuestId && stay.PropertyId == caller.PropertyId && stay.Status is StayStatus.Reserved or StayStatus.InHouse
            ? stay
            : null;
    }

    /// <summary>
    /// The conversation, if it belongs to the caller's stay, which must still be active. Another stay's conversation is not
    /// found, so guests cannot learn which IDs exist.
    /// </summary>
    private async Task<Result<Conversation>> OwnAsync(GuestCaller caller, Guid conversationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (await ActiveStayAsync(caller, cancellationToken).ConfigureAwait(false) is null)
        {
            return ConversationErrors.StayNotActive;
        }

        // An empty GUID cannot be a conversation ID, so it names no conversation.
        Conversation? conversation = conversationId == Guid.Empty
            ? null
            : await conversations.GetByIdAsync(new ConversationId(conversationId), cancellationToken).ConfigureAwait(false);

        return conversation is not null && conversation.StayContextId == caller.StayContextId
            ? conversation
            : ConversationErrors.ConversationNotFound;
    }
}
