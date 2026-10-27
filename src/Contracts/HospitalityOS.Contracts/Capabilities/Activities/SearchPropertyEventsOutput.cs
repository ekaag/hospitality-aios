using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Capabilities.Activities;

/// <summary>
/// Output of <c>Events.SearchPropertyEvents</c>; past and cancelled events are never included.
/// </summary>
/// <param name="TimeZone">IANA time zone of the property.</param>
/// <param name="LocalNow">The current property-local time.</param>
/// <param name="Events">Matching events, earliest first.</param>
public sealed record SearchPropertyEventsOutput(
    string TimeZone,
    DateTime LocalNow,
    ValueList<EventEntry> Events);
