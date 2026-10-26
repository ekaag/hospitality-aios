using HospitalityOS.Eventing.Contracts;

namespace HospitalityOS.Eventing.Handling;

/// <summary>
/// One registered handler: its stable name (transport subscription and inbox key) and the event type and version it
/// handles.
/// </summary>
internal sealed record EventHandlerRegistration(string Name, EventTypeName Event);
