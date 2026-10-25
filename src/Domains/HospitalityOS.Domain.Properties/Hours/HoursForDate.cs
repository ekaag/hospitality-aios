using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Domain.Properties.Hours;

/// <summary>
/// The resolved hours for one local date: the periods that open on that date, in order.
/// </summary>
/// <param name="Date">The property-local date.</param>
/// <param name="Source">Which rule decided.</param>
/// <param name="Periods">Opening periods starting on the date; empty when closed. A period may end the next day.</param>
/// <param name="Note">The override note or closure reason, when there is one.</param>
public sealed record HoursForDate(DateOnly Date, HoursSource Source, ValueList<OpenPeriod> Periods, string? Note = null)
{
    /// <summary>
    /// Whether nothing opens on the date. (A period from the previous evening may still run into it.)
    /// </summary>
    public bool IsClosed => Periods.Count == 0;
}
