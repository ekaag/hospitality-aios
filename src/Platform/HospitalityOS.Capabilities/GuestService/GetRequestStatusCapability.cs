using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Capabilities.Reading;
using HospitalityOS.Contracts.Capabilities.GuestService;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.Domain.Services.ServiceRequests;
using HospitalityOS.Domain.Services.Services;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Capabilities.GuestService;

/// <summary>
/// <c>GuestService.GetRequestStatus</c>: a stay's unresolved service requests and their canonical status (Journey 12), so
/// the agent reports where a request is from its state and that of the latest staff work for it (Journey 12) rather than
/// guessing, and never invents when it will be done.
/// </summary>
internal sealed class GetRequestStatusCapability(
    IStayContextRepository stays,
    IServiceRequestRepository requests,
    IServiceRepository services,
    IHumanActionRepository actions,
    IPropertyRepository properties,
    TimeProvider timeProvider) : ICapability<GetRequestStatusInput, GetRequestStatusOutput>
{
    public static CapabilityDescriptor Descriptor { get; } = ReadCapability.Describe(
        "GuestService.GetRequestStatus",
        "Returns a stay's unresolved service requests (deliveries and repairs) with their canonical status: Open, InProgress, or "
            + "Exception, and where the staff work for it is (workStatus: Requested, Assigned, Accepted, InProgress). Report only those; never estimate when a request will be done. An empty list means nothing is pending.");

    public async Task<Result<GetRequestStatusOutput>> ExecuteAsync(GetRequestStatusInput input, CancellationToken cancellationToken)
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

        IReadOnlyList<ServiceRequest> active = await requests.ListActiveForStayAsync(stay.Id, cancellationToken).ConfigureAwait(false);
        Dictionary<ServiceId, Service?> byId = [];

        foreach (ServiceId serviceId in active.Select(request => request.ServiceId).Distinct())
        {
            byId[serviceId] = await services.GetByIdAsync(serviceId, cancellationToken).ConfigureAwait(false);
        }

        Dictionary<ServiceRequestId, HumanAction?> latestWork = [];

        foreach (ServiceRequest request in active)
        {
            IReadOnlyList<HumanAction> work = await actions.ListForServiceRequestAsync(request.Id, cancellationToken).ConfigureAwait(false);
            latestWork[request.Id] = work.Count == 0 ? null : work[^1];
        }

        return new GetRequestStatusOutput(ValueList.From(active
            .OrderByDescending(request => request.CreatedAt)
            .Select(request => RequestEntries.Of(request, byId[request.ServiceId], clock.Value.TimeZone, latestWork[request.Id]))));
    }
}
