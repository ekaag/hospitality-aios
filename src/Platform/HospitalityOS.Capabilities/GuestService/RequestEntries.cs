using HospitalityOS.Capabilities.Reading;
using HospitalityOS.Contracts;
using HospitalityOS.Contracts.Capabilities.GuestService;
using HospitalityOS.Contracts.Capabilities.HumanActions;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Domain.Services.ServiceRequests;
using HospitalityOS.Domain.Services.Services;

namespace HospitalityOS.Capabilities.GuestService;

/// <summary>
/// Service requests as the guest service capabilities return them.
/// </summary>
internal static class RequestEntries
{
    /// <param name="request">The request.</param>
    /// <param name="service">Its service, if it still exists.</param>
    /// <param name="timeZone">The property's time zone.</param>
    /// <param name="work">The latest staff work for it, if staff have been asked.</param>
    public static ServiceRequestEntry Of(ServiceRequest request, Service? service, TimeZoneInfo timeZone, HumanAction? work = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        ServiceItem? item = service?.Details.Items?.FirstOrDefault(candidate => candidate.Code == request.Details.ItemCode);

        return new ServiceRequestEntry(
            Sources.Of(request),
            service?.Details.Code ?? "",
            service?.Details.Name ?? "",
            request.Details.ItemCode,
            item?.Name,
            request.Details.Quantity,
            request.Details.Description,
            request.Details.Location.Label,
            EnumMirror.To<RequestStatus>(request.Status),
            TimeZoneInfo.ConvertTime(request.CreatedAt, timeZone).DateTime,
            work is null ? null : EnumMirror.To<ActionStatus>(work.Status));
    }
}
