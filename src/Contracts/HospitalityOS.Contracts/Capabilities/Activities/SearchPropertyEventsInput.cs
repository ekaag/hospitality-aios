using System.ComponentModel;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Capabilities.Activities;

/// <summary>
/// Input of <c>Events.SearchPropertyEvents</c>.
/// </summary>
/// <param name="PropertyId">The property.</param>
/// <param name="From">Only events still running after this property-local time; now when omitted.</param>
/// <param name="To">Only events starting before this property-local time.</param>
/// <param name="Category">Only this kind.</param>
/// <param name="Audience">Only events for this audience (events for everyone always match).</param>
/// <param name="Limit">Most events to return, 1 to 50.</param>
public sealed record SearchPropertyEventsInput(
    [property: Description("The property.")] PropertyId PropertyId,
    [property: Description("Only events still running after this property-local time; now when omitted.")] DateTime? From = null,
    [property: Description("Only events starting before this property-local time.")] DateTime? To = null,
    [property: Description("Only this kind.")] PropertyEventCategory? Category = null,
    [property: Description("Only events for this audience (events for everyone always match).")] EventAudience? Audience = null,
    [property: Description("Most events to return, 1 to 50.")] int Limit = 10);
