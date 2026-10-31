namespace HospitalityOS.Domain.Guests.GuestMessages;

/// <summary>
/// What a guest's message asks for, which decides the controller that handles it.
/// </summary>
public enum MessageIntent
{
    /// <summary>
    /// A question about the property, answered by the concierge from authoritative sources.
    /// </summary>
    Question,

    /// <summary>
    /// Something physical to be done, e.g. towels delivered or the AC repaired, handled by the Guest Service Controller.
    /// </summary>
    ServiceRequest,

    /// <summary>
    /// How the guest's earlier requests are going, answered by the concierge from their canonical state (Journey 12).
    /// </summary>
    RequestStatus,
}
