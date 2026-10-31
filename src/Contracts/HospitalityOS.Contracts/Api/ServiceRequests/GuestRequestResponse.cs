using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.ServiceRequests;

/// <summary>
/// One of a guest's unresolved requests, as the guest sees it: what, where, and where it is, but not who is doing it.
/// </summary>
/// <param name="Id">The request.</param>
/// <param name="ServiceName">The service, e.g. <c>Housekeeping amenities</c>.</param>
/// <param name="ItemName">The item, e.g. <c>Bath towel</c>, for a service with items.</param>
/// <param name="Quantity">How many.</param>
/// <param name="Description">The request in the guest's words, for a service without items.</param>
/// <param name="Location">Where it is delivered or done, e.g. <c>Room 412</c>.</param>
/// <param name="Status">Where it is.</param>
/// <param name="LocalRequestedAt">When it was requested, in the property's time.</param>
public sealed record GuestRequestResponse(
    ServiceRequestId Id,
    string ServiceName,
    string? ItemName,
    int Quantity,
    string? Description,
    string Location,
    GuestRequestStatus Status,
    DateTime LocalRequestedAt);
