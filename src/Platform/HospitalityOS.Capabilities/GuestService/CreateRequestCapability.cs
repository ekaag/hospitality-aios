using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Capabilities.Reading;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Capabilities.GuestService;
using HospitalityOS.Domain.Guests.GuestMessages;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.Domain.Properties.AccommodationUnits;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.Domain.Services.ServiceRequests;
using HospitalityOS.Domain.Services.Services;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Locations;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Capabilities.GuestService;

/// <summary>
/// <c>GuestService.CreateRequest</c>: records a physical-service request for an in-house guest, delivered to or done in
/// their room (Journeys 9 to 11), raising <c>ServiceRequestCreated</c>. The request is checked against the service (it
/// must be guest-requestable, the item one it offers, the quantity within its limit). A message gets at most one request:
/// asking again for the same message returns the request already created, so a redelivered message cannot cause a second
/// delivery (P8).
/// </summary>
internal sealed class CreateRequestCapability(
    IStayContextRepository stays,
    IAccommodationUnitRepository units,
    IServiceRepository services,
    IServiceRequestRepository requests,
    IGuestMessageRepository messages,
    IPropertyRepository properties,
    IBindingResolver<IIdGenerator> idGenerators,
    TimeProvider timeProvider) : ICapability<CreateRequestInput, CreateRequestOutput>
{
    public static CapabilityDescriptor Descriptor { get; } = new()
    {
        Name = "GuestService.CreateRequest",
        Version = 1,
        Description = "Records a guest's request for something physical to be done in their room, such as delivering towels or a pillow "
            + "(a service with items) or repairing the air conditioning (a service without items, described in the guest's words). "
            + "Staff are then asked to do it; no approval is needed.",
        Authorization = CapabilityAuthorization.Allow(CapabilityCaller.Agent, CapabilityCaller.Worker, CapabilityCaller.Manager, CapabilityCaller.Admin),
        ConfirmationPolicy = ConfirmationPolicy.NotRequired,
        SideEffects = SideEffect.PlatformState,
        Idempotency = IdempotencyPolicy.KeyRequired,
        AuditClassification = AuditClassification.Routine,
    };

    public async Task<Result<CreateRequestOutput>> ExecuteAsync(CreateRequestInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        StayContext? stay = await stays.GetByIdAsync(input.StayContextId, cancellationToken).ConfigureAwait(false);

        if (stay is null)
        {
            return GuestServiceErrors.StayNotFound;
        }

        Result<PropertyClock> clock = await PropertyClock.LoadAsync(properties, stay.PropertyId, timeProvider, cancellationToken).ConfigureAwait(false);

        if (clock.IsFailure)
        {
            return clock.Error;
        }

        Service? service = await services.FindByCodeAsync(stay.PropertyId, input.ServiceCode.Trim(), cancellationToken).ConfigureAwait(false);

        if (service is null)
        {
            return GuestServiceErrors.ServiceNotFound(input.ServiceCode);
        }

        if (input.SourceMessageId is { } messageId
            && await requests.FindBySourceMessageAsync(messageId, cancellationToken).ConfigureAwait(false) is { } existing)
        {
            return new CreateRequestOutput(RequestEntries.Of(existing, service, clock.Value.TimeZone), Created: false);
        }

        if (stay.Status != StayStatus.InHouse || stay.AccommodationUnitId is not { } unitId
            || await units.GetByIdAsync(unitId, cancellationToken).ConfigureAwait(false) is not { } unit)
        {
            return GuestServiceErrors.StayNotInHouse;
        }

        ConversationId? conversationId = null;

        if (input.SourceMessageId is { } sourceId)
        {
            GuestMessage? source = await messages.GetByIdAsync(sourceId, cancellationToken).ConfigureAwait(false);

            if (source is null || source.StayContextId != stay.Id)
            {
                return GuestServiceErrors.MessageNotFound;
            }

            conversationId = source.ConversationId;
        }

        ServiceRequestDetails details = new(
            new ActionLocation($"Room {unit.UnitNumber}", unit.Id, unit.SpaceId),
            string.IsNullOrWhiteSpace(input.ItemCode) ? null : input.ItemCode.Trim(),
            input.Quantity,
            string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim());
        Result<ServiceRequest> created = ServiceRequest.Create(
            new ServiceRequestId(idGenerators.Resolve().NewId()),
            service,
            details,
            new RequestOrigin(stay.GuestId, stay.Id, conversationId, input.SourceMessageId),
            timeProvider.GetUtcNow());

        if (created.IsFailure)
        {
            return created.Error;
        }

        await requests.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);

        return new CreateRequestOutput(RequestEntries.Of(created.Value, service, clock.Value.TimeZone), Created: true);
    }
}
