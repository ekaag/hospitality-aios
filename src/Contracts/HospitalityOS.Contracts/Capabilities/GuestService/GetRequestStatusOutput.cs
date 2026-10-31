using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Capabilities.GuestService;

/// <summary>
/// Output of <c>GuestService.GetRequestStatus</c>; empty means the stay has no open requests.
/// </summary>
/// <param name="Requests">The stay's unresolved requests (open, in progress, or in exception), newest first.</param>
public sealed record GetRequestStatusOutput(ValueList<ServiceRequestEntry> Requests);
