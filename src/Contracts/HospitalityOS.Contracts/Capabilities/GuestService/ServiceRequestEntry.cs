using HospitalityOS.Contracts.Capabilities.HumanActions;

namespace HospitalityOS.Contracts.Capabilities.GuestService;

/// <summary>
/// A guest's service request as it stands.
/// </summary>
/// <param name="Source">The request record; its ID is the request ID.</param>
/// <param name="ServiceCode">The service, e.g. <c>housekeeping.amenities</c>.</param>
/// <param name="ServiceName">The service's name.</param>
/// <param name="ItemCode">The item, e.g. <c>bath_towel</c>, when the service has items.</param>
/// <param name="ItemName">The item's name.</param>
/// <param name="Quantity">How many.</param>
/// <param name="Description">The request in the guest's words, for services without items.</param>
/// <param name="Location">Where it is delivered or done, e.g. <c>Room 412</c>.</param>
/// <param name="Status">Where it is; report only this, never an estimate of when it will be done.</param>
/// <param name="LocalRequestedAt">When it was requested (property-local).</param>
/// <param name="WorkStatus">Where the staff work for it is (Requested, Assigned, Accepted, InProgress, ...), when staff
/// have been asked; report only this, never an estimate.</param>
public sealed record ServiceRequestEntry(
    SourceReference Source,
    string ServiceCode,
    string ServiceName,
    string? ItemCode,
    string? ItemName,
    int Quantity,
    string? Description,
    string Location,
    RequestStatus Status,
    DateTime LocalRequestedAt,
    ActionStatus? WorkStatus = null);
