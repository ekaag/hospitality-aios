using System.Text.Json;
using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Contracts.Capabilities.HumanActions;
using HospitalityOS.Contracts.Events;
using HospitalityOS.Contracts.Events.Services;
using HospitalityOS.Contracts.Events.Values;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Domain.Services.ServiceRequests;
using HospitalityOS.Domain.Services.Services;
using HospitalityOS.Eventing.Handling;
using HospitalityOS.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace HospitalityOS.Workflows.GuestService;

/// <summary>
/// The Guest Service Controller's second step (spec §9, Journeys 9 to 11): asks staff to do the work each new
/// <c>ServiceRequest</c> needs, through <c>HumanAction.Create</c> with the request as its idempotency key (P7, P8), so a
/// redelivered event cannot ask twice. A request for items becomes a delivery of those items; one described in words
/// becomes a check of the reported problem. The work is for the request's team (the service's category, as the skill the
/// router matches) and due when the request is. If the work cannot be requested, the request is put into exception, so
/// staff see it rather than it waiting for nobody (Journey 20).
/// </summary>
/// <remarks>
/// No model is involved: everything the work needs is in the request and its service.
/// </remarks>
internal sealed partial class WorkRequester(
    IServiceRepository services,
    IServiceRequestRepository requests,
    ICapabilityExecutor executor,
    TimeProvider timeProvider,
    ILogger<WorkRequester> logger) : IEventHandler<ServiceRequestCreatedV1>
{
    /// <summary>
    /// The handler's name, so its deliveries act as that agent and the work records it as who asked.
    /// </summary>
    public const string Name = "guestservice.request_work";

    public const string CreateActionCapability = "HumanAction.Create";

    public async Task HandleAsync(EventEnvelope envelope, ServiceRequestCreatedV1 payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(payload);

        // The service was committed before the request for it, so it cannot be missing.
        Service service = await services.GetByIdAsync(new ServiceId(payload.ServiceId), cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Service {payload.ServiceId} does not exist.");
        DateTimeOffset now = timeProvider.GetUtcNow();

        CapabilityRequest create = new(
            CreateActionCapability,
            JsonSerializer.SerializeToElement(Work(envelope, payload, service.Details, now), ContractJson.Options),
            1,
            IdempotencyKey: $"service-request:{payload.ServiceRequestId.Value}");
        Result<CapabilityResponse> created = await executor.ExecuteAsync(create, cancellationToken).ConfigureAwait(false);

        if (created.IsSuccess)
        {
            return;
        }

        LogRefused(payload.ServiceRequestId.Value, created.Error.Code);
        ServiceRequest request = await requests.GetByIdAsync(payload.ServiceRequestId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Service request {payload.ServiceRequestId} does not exist.");

        // Only a request resolved meanwhile refuses an exception, and then there is nothing left to do.
        request.RecordException($"Staff could not be asked to do the work ({created.Error.Code}).", now);
    }

    /// <summary>
    /// The work a request needs, as staff will read it.
    /// </summary>
    public static CreateActionInput Work(EventEnvelope envelope, ServiceRequestCreatedV1 payload, ServiceDetails service, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(service);

        ServiceRequestDetailsData details = payload.Details;
        ServiceItem? item = details.ItemCode is { } code ? service.Items?.FirstOrDefault(candidate => candidate.Code == code) : null;
        string reason = payload.Origin.SourceMessageId is null
            ? $"{service.Name} was requested."
            : "The guest asked for it in a message to the concierge.";

        return new CreateActionInput(
            payload.PropertyId,
            item is null ? ActionType.CheckRoom : ActionType.DeliverItems,
            item is null ? $"{service.Name}: {details.Description ?? "check the reported problem"}" : $"Deliver {details.Quantity} × {item.Name}",
            details.Location,
            reason,
            ActionPriority.Normal,
            Deadline: payload.DueAt > now ? payload.DueAt : null,
            Items: item is null ? null : [new ActionItemEntry(item.Name, details.Quantity, item.Code)],
            SkillsRequired: Skill(service.Category) is { } skill ? [skill] : null,
            ServiceRequestId: payload.ServiceRequestId,
            SourceEventId: envelope.EventId);
    }

    /// <summary>
    /// The skill a service's team has, which the router matches; none for uncategorized services.
    /// </summary>
    private static string? Skill(ServiceCategory category) => category switch
    {
        ServiceCategory.Housekeeping => "housekeeping",
        ServiceCategory.Maintenance => "maintenance",
        ServiceCategory.FoodAndBeverage => "food_and_beverage",
        ServiceCategory.Concierge => "concierge",
        ServiceCategory.Transport => "transport",
        _ => null,
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Staff could not be asked to do the work for service request {ServiceRequestId} ({ErrorCode}); the request is in exception")]
    private partial void LogRefused(Guid serviceRequestId, string errorCode);
}
