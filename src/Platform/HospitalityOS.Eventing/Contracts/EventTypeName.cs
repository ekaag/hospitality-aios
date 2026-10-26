namespace HospitalityOS.Eventing.Contracts;

/// <summary>
/// An event type at one schema version, as the envelope names it: <c>eventType</c> + <c>eventVersion</c>.
/// </summary>
internal readonly record struct EventTypeName(string EventType, int Version)
{
    public override string ToString() => $"{EventType} v{Version}";
}
