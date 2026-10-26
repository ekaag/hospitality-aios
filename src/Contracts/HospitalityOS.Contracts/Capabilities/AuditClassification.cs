namespace HospitalityOS.Contracts.Capabilities;

/// <summary>
/// How much of an invocation its audit record may keep (spec §11 <c>auditClassification</c>). Every invocation is
/// audited (spec §35); the classification decides only the detail recorded.
/// </summary>
public enum AuditClassification
{
    /// <summary>
    /// Inputs and outcome are recorded.
    /// </summary>
    Routine,

    /// <summary>
    /// Handles guest personal data or security-relevant changes: the outcome is recorded, inputs and outputs are not
    /// (P10).
    /// </summary>
    Sensitive,
}
