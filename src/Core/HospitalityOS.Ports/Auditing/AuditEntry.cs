namespace HospitalityOS.Ports.Auditing;

/// <summary>
/// A saved audit record and its place in the audit log.
/// </summary>
/// <param name="Position">Log position, increasing in commit order; use it to page.</param>
/// <param name="Record">The record.</param>
public sealed record AuditEntry(long Position, AuditRecord Record);
