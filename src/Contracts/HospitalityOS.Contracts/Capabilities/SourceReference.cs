namespace HospitalityOS.Contracts.Capabilities;

/// <summary>
/// The authoritative platform record an answer comes from (P4). Agents cite it, and traces record it, so every
/// statement can be checked against the record and version it came from; an answer with no source is unknown.
/// </summary>
/// <param name="Type">The kind of record, e.g. <c>Policy</c> or <c>OperatingHours</c>.</param>
/// <param name="Id">The record.</param>
/// <param name="Version">The record's version when it was read; it increases with every change.</param>
public sealed record SourceReference(
    string Type,
    Guid Id,
    long Version);
