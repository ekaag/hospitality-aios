using System.Globalization;
using System.Text;
using System.Text.Json;
using HospitalityOS.Ai.StructuredOutput;
using HospitalityOS.Ai.Tracing;
using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Capabilities.GuestService;
using HospitalityOS.Contracts.Events;
using HospitalityOS.Contracts.Events.Guests;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Domain.Capabilities.AITraces;
using HospitalityOS.Domain.Guests.Conversations;
using HospitalityOS.Domain.Guests.GuestMessages;
using HospitalityOS.Domain.Services.Services;
using HospitalityOS.Eventing.Handling;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace HospitalityOS.Workflows.GuestService;

/// <summary>
/// The Guest Service Controller's first step (spec §9, Journeys 9 to 11): turns each guest message triaged as a service
/// request into a canonical <c>ServiceRequest</c>. The model reads the message against the property's guest-requestable
/// services (<c>guestservice.extract</c>) for the service, item, quantity, or problem; the controller then creates the
/// request through <c>GuestService.CreateRequest</c> for the message's stay, with the message as its idempotency key (P7,
/// P8), and acknowledges it to the guest in its own words, never the model's, without promising when it will be done. A
/// message it cannot understand gets a request for more detail; one from a guest who is not in house, or that the
/// capability refuses, is passed to the front desk. Everything but the request itself commits with the inbox record.
/// </summary>
internal sealed partial class GuestServiceController(
    IGuestMessageRepository messages,
    IConversationRepository conversations,
    IServiceRepository services,
    ITypedExtractor extractor,
    ICapabilityExecutor executor,
    IAITraceRecorder traces,
    IBindingResolver<IIdGenerator> idGenerators,
    TimeProvider timeProvider,
    ILogger<GuestServiceController> logger) : IEventHandler<GuestMessageTriagedV1>
{
    /// <summary>
    /// The extraction task, and the handler's name, so its deliveries act as that agent.
    /// </summary>
    public const string Task = "guestservice.extract";

    public const string CreateRequestCapability = "GuestService.CreateRequest";
    public const string Intent = "service_request";

    public const string ClarifyAnswer =
        "I'm not sure what you'd like us to bring or fix. Could you tell me a little more, or ask the front desk?";

    public const string NoRoomAnswer =
        "I can arrange that once you're checked in to your room. In the meantime, the front desk will be glad to help.";

    public const string RefusedAnswer =
        "I couldn't arrange that automatically, so please ask the front desk, who will be glad to help.";

    public const string UnavailableAnswer =
        "I'm sorry, I can't arrange that right now. Please try again in a moment, or ask the front desk.";

    public async Task HandleAsync(EventEnvelope envelope, GuestMessageTriagedV1 payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (payload.Intent != nameof(MessageIntent.ServiceRequest))
        {
            return;
        }

        // Both were committed before the message was triaged, so neither can be missing.
        GuestMessage request = await messages.GetByIdAsync(payload.GuestMessageId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Guest message {payload.GuestMessageId} does not exist.");
        Conversation conversation = await conversations.GetByIdAsync(payload.ConversationId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Conversation {payload.ConversationId} does not exist.");

        if (conversation.Status == ConversationStatus.Closed)
        {
            return;
        }

        // A request is delivered to the stay's room, so an anonymous guest is sent to the front desk without asking the model.
        if (payload.StayContextId is not { } stay)
        {
            await ReplyAsync(conversation, NoRoomAnswer, cancellationToken).ConfigureAwait(false);

            return;
        }

        AITraceTrigger trigger = new(AITraceTrigger.GuestMessage, request.Id.Value, request.Text);
        IReadOnlyList<Service> offered = [.. (await services.ListForPropertyAsync(request.PropertyId, cancellationToken).ConfigureAwait(false))
            .Where(service => service.Details.IsGuestRequestable)];
        Result<TypedExtraction<ServiceRequestExtraction>> extracted = await extractor.ExtractAsync<ServiceRequestExtraction>(
            Task, request.Text, new Dictionary<string, string> { ["services"] = Describe(offered) }, cancellationToken).ConfigureAwait(false);

        if (extracted.IsFailure)
        {
            LogUnavailable(request.Id.Value, extracted.Error.Code);
            await FinishAsync(trigger, request, AITraceOutcome.Failed, UnavailableAnswer, null, conversation, cancellationToken).ConfigureAwait(false);

            return;
        }

        ServiceRequestExtraction understood = extracted.Value.Value;

        if (!understood.Understood || string.IsNullOrWhiteSpace(understood.ServiceCode))
        {
            await FinishAsync(trigger, request, AITraceOutcome.Uncertain, ClarifyAnswer, null, conversation, cancellationToken).ConfigureAwait(false);

            return;
        }

        CapabilityRequest create = new(
            CreateRequestCapability,
            JsonSerializer.SerializeToElement(
                new CreateRequestInput(stay, understood.ServiceCode, understood.ItemCode, understood.Quantity ?? 1, understood.Description, request.Id),
                ContractJson.Options),
            1,
            IdempotencyKey: $"guest-message:{request.Id.Value}");
        Result<CapabilityResponse> created = await executor.ExecuteAsync(create, cancellationToken).ConfigureAwait(false);
        traces.RecordAction(create, created);

        if (created.IsFailure)
        {
            LogRefused(request.Id.Value, created.Error.Code);
            await FinishAsync(trigger, request, AITraceOutcome.Failed, RefusedAnswer, null, conversation, cancellationToken).ConfigureAwait(false);

            return;
        }

        ServiceRequestEntry entry = created.Value.Output.Deserialize<CreateRequestOutput>(ContractJson.Options)!.Request;
        await FinishAsync(trigger, request, AITraceOutcome.Answered, Acknowledgement(entry), entry.Source, conversation, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// What the guest is told once the request is recorded: what will be done and where, never when.
    /// </summary>
    public static string Acknowledgement(ServiceRequestEntry request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ItemName is { } item)
        {
            string name = item.ToLower(CultureInfo.CurrentCulture);
            string what = request.Quantity == 1
                ? $"{("aeiou".Contains(name[0], StringComparison.Ordinal) ? "an" : "a")} {name}"
                : $"{request.Quantity} {(name.EndsWith('s') ? name : name + "s")}";

            return $"Of course. I've asked our team to bring {what} to {request.Location}.";
        }

        return $"Thanks for letting us know. I've asked our team to take care of it in {request.Location}.";
    }

    /// <summary>
    /// The services the model may choose from, one per line.
    /// </summary>
    private static string Describe(IEnumerable<Service> offered)
    {
        StringBuilder lines = new();

        foreach (Service service in offered)
        {
            ServiceDetails details = service.Details;
            lines.Append(CultureInfo.InvariantCulture, $"- {details.Code}: {details.Name}");
            lines.Append(details.Description is { } description ? $", {description}" : "");

            if (details.Items is { Count: > 0 } items)
            {
                lines.Append("; items: ");
                lines.AppendJoin(", ", items.Select(item => $"{item.Code} ({item.Name}, at most {item.MaxQuantityPerRequest})"));
            }
            else
            {
                lines.Append("; no items: describe the problem");
            }

            lines.Append('\n');
        }

        return lines.ToString();
    }

    private async Task FinishAsync(
        AITraceTrigger trigger,
        GuestMessage request,
        AITraceOutcome outcome,
        string reply,
        SourceReference? cited,
        Conversation conversation,
        CancellationToken cancellationToken)
    {
        await traces.CompleteAsync(
            new AITraceCompletion(trigger, outcome, reply, cited is null ? [] : [cited], request.PropertyId, Intent),
            cancellationToken).ConfigureAwait(false);
        await ReplyAsync(conversation, reply, cancellationToken).ConfigureAwait(false);
    }

    private async Task ReplyAsync(Conversation conversation, string text, CancellationToken cancellationToken)
    {
        Result<GuestMessage> reply = GuestMessage.Record(
            new GuestMessageId(idGenerators.Resolve().NewId()), conversation, MessageSender.Concierge, text, null, timeProvider.GetUtcNow());

        // The texts are the controller's own, so only a conversation closed meanwhile can refuse one; then nobody is told.
        if (reply.IsSuccess)
        {
            await messages.AddAsync(reply.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The service request in guest message {MessageId} could not be read ({ErrorCode}); the guest was told to try again")]
    private partial void LogUnavailable(Guid messageId, string errorCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The service request in guest message {MessageId} was refused ({ErrorCode}); the guest was sent to the front desk")]
    private partial void LogRefused(Guid messageId, string errorCode);
}
