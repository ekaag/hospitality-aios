namespace HospitalityOS.Contracts.Capabilities;

/// <summary>
/// How a capability keeps a repeated invocation from repeating its effect (spec §11 <c>idempotency</c>, P8).
/// </summary>
public enum IdempotencyPolicy
{
    /// <summary>
    /// Repeating it has no further effect by nature: reads, and changes that are no-ops once made (e.g. completing a
    /// completed HumanAction).
    /// </summary>
    Inherent,

    /// <summary>
    /// Every invocation carries an idempotency key; a repeated key returns the first invocation's result without
    /// running it again.
    /// </summary>
    KeyRequired,
}
