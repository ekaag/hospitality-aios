namespace HospitalityOS.Contracts.Capabilities.GuestService;

/// <summary>
/// Output of <c>GuestService.CreateRequest</c>.
/// </summary>
/// <param name="Request">The request: new, or the one already created for the same message.</param>
/// <param name="Created">Whether this call created it.</param>
public sealed record CreateRequestOutput(ServiceRequestEntry Request, bool Created);
