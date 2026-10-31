using System.Text.Json;
using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Contracts.Capabilities.Messaging;
using HospitalityOS.Contracts.Events;
using HospitalityOS.Contracts.Events.Services;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Domain.Guests.Conversations;
using HospitalityOS.Domain.Guests.GuestMessages;
using HospitalityOS.Domain.Services.ServiceRequests;
using HospitalityOS.Domain.Services.Services;
using HospitalityOS.Eventing.Handling;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace HospitalityOS.Workflows.GuestService;

/// <summary>
/// The Guest Service Controller's last step (spec §9, Journey 9): tells the guest their request is done, in its own words,
/// through <c>Messaging.NotifyGuest</c> keyed by the request, so the guest is told once however often completion is
/// delivered (Journey 19, P8). On the web channel the conversation is where the guest reads it, so a message the provider
/// accepted is also added to the conversation. A request made without a conversation, e.g. by staff, has nobody to tell.
/// </summary>
internal sealed partial class CompletionNotifier(
    IServiceRequestRepository requests,
    IServiceRepository services,
    IConversationRepository conversations,
    IGuestMessageRepository messages,
    ICapabilityExecutor executor,
    IBindingResolver<IIdGenerator> idGenerators,
    TimeProvider timeProvider,
    ILogger<CompletionNotifier> logger) : IEventHandler<ServiceRequestCompletedV1>
{
    /// <summary>
    /// The handler's name, so its deliveries act as that agent.
    /// </summary>
    public const string Name = "guestservice.notify_completion";

    public const string NotifyCapability = "Messaging.NotifyGuest";

    public async Task HandleAsync(EventEnvelope envelope, ServiceRequestCompletedV1 payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (payload.Origin.ConversationId is not { } conversationId)
        {
            return;
        }

        // Both were committed before the request was completed, so neither can be missing.
        ServiceRequest request = await requests.GetByIdAsync(payload.ServiceRequestId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Service request {payload.ServiceRequestId} does not exist.");
        Conversation conversation = await conversations.GetByIdAsync(conversationId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Conversation {conversationId} does not exist.");
        Service? service = await services.GetByIdAsync(request.ServiceId, cancellationToken).ConfigureAwait(false);
        string text = Done(request, service);

        Result<CapabilityResponse> notified = await executor.ExecuteAsync(
            new CapabilityRequest(
                NotifyCapability,
                JsonSerializer.SerializeToElement(
                    new NotifyGuestInput(conversationId, text, Key(request.Id), nameof(ServiceRequest), request.Id.Value), ContractJson.Options),
                1,
                IdempotencyKey: Key(request.Id)),
            cancellationToken).ConfigureAwait(false);

        if (notified.IsFailure)
        {
            LogNotNotified(request.Id.Value, notified.Error.Code);

            return;
        }

        NotifyGuestOutput output = notified.Value.Output.Deserialize<NotifyGuestOutput>(ContractJson.Options)!;

        if (output.Status != NotificationStatus.Sent)
        {
            LogNotNotified(request.Id.Value, output.FailureReason ?? output.Status.ToString());

            return;
        }

        if (conversation.Channel == ConversationChannel.Web
            && GuestMessage.Record(new GuestMessageId(idGenerators.Resolve().NewId()), conversation, MessageSender.Concierge, text, null, timeProvider.GetUtcNow()) is { IsSuccess: true } shown)
        {
            await messages.AddAsync(shown.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// What the guest is told when their request is done: what and where.
    /// </summary>
    public static string Done(ServiceRequest request, Service? service)
    {
        ArgumentNullException.ThrowIfNull(request);

        string location = request.Details.Location.Label;

        if (request.Details.ItemCode is { } code && service?.FindItem(code) is { } item)
        {
            return request.Details.Quantity == 1
                ? $"Good news: your {GuestServiceController.Items(item.Name, 1, article: false)} has been delivered to {location}."
                : $"Good news: your {GuestServiceController.Items(item.Name, request.Details.Quantity, article: false)} have been delivered to {location}.";
        }

        return $"Good news: what you reported in {location} has been taken care of. Thank you for your patience.";
    }

    private static string Key(ServiceRequestId id) => $"service-request-completed:{id.Value}";

    [LoggerMessage(Level = LogLevel.Warning, Message = "The guest was not told service request {ServiceRequestId} is done ({Reason})")]
    private partial void LogNotNotified(Guid serviceRequestId, string reason);
}
