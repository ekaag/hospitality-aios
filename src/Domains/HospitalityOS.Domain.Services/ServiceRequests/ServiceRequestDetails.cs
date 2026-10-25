using HospitalityOS.SharedKernel.Locations;

namespace HospitalityOS.Domain.Services.ServiceRequests;

/// <summary>
/// What is being requested (Journey 9: service type, item, quantity, location). Validated against the
/// service by <see cref="ServiceRequest"/>.
/// </summary>
/// <param name="Location">Where to deliver or work, e.g. Room 412; required.</param>
/// <param name="ItemCode">The service item, e.g. <c>bath_towel</c>; required when the service lists items.</param>
/// <param name="Quantity">How many; 1 unless the guest said otherwise (Journey 10).</param>
/// <param name="Description">The request in words, e.g. "The AC isn't working"; required when the service has no items.</param>
public sealed record ServiceRequestDetails(
    ActionLocation Location,
    string? ItemCode = null,
    int Quantity = 1,
    string? Description = null);
