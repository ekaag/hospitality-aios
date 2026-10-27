using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Capabilities;

/// <summary>
/// The opening hours of one subject on one property-local date.
/// </summary>
/// <param name="Source">The hours record.</param>
/// <param name="Date">The property-local date.</param>
/// <param name="Basis">Which rule decided them.</param>
/// <param name="Periods">The opening periods; empty means closed all day.</param>
/// <param name="Note">An explanation, e.g. the closure reason.</param>
/// <param name="IsOpenNow">Whether the subject is open at the time of the query (including overnight hours from the day before).</param>
public sealed record HoursOnDate(
    SourceReference Source,
    DateOnly Date,
    HoursBasis Basis,
    ValueList<OpeningPeriod> Periods,
    string? Note,
    bool IsOpenNow);
