namespace HospitalityOS.Domain.Capabilities.CapabilityInvocations;

/// <summary>
/// Where an invocation is: <see cref="Requested"/> → <see cref="Succeeded"/> or <see cref="Failed"/>.
/// </summary>
public enum CapabilityInvocationStatus
{
    /// <summary>
    /// Accepted and running.
    /// </summary>
    Requested,

    /// <summary>
    /// Ran and produced its output.
    /// </summary>
    Succeeded,

    /// <summary>
    /// Ran and failed; nothing it attempted was saved.
    /// </summary>
    Failed,
}
