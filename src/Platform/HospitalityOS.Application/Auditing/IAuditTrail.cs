using HospitalityOS.Ports.Auditing;

namespace HospitalityOS.Application.Auditing;

/// <summary>
/// Records audited actions (P10) with everything the scope already knows filled in: tenant, actor, correlation,
/// causation, time, and a new ID. The record is saved with the scope's commit. Scoped.
/// </summary>
public interface IAuditTrail
{
    /// <summary>
    /// Stages an audit record of an action in the current scope.
    /// </summary>
    /// <param name="action">What was attempted, e.g. <c>GuestService.CreateRequest</c>; required.</param>
    /// <param name="outcome">How it ended.</param>
    /// <param name="target">What it was performed on, if anything.</param>
    /// <param name="detail">A short, redacted explanation; never secrets or guest personal data.</param>
    void Record(string action, AuditOutcome outcome, AuditTarget? target = null, string? detail = null);
}
