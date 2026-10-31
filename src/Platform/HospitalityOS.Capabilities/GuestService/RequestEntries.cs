using HospitalityOS.Capabilities.Reading;
using HospitalityOS.Contracts;
using HospitalityOS.Contracts.Capabilities.GuestService;
using HospitalityOS.Domain.Services.ServiceRequests;
using HospitalityOS.Domain.Services.Services;

namespace HospitalityOS.Capabilities.GuestService;

/// <summary>
/// Service requests as the guest service capabilities return them.
/// </summary>
internal static class RequestEntries
{
    public static ServiceRequestEntry Of(ServiceRequest request, Service? service, TimeZoneInfo timeZone)
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
            TimeZoneInfo.ConvertTime(request.CreatedAt, timeZone).DateTime);
    }
}
