using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Capabilities.CapabilityInvocations;

/// <summary>
/// Identifies a <see cref="CapabilityInvocation"/>.
/// </summary>
public readonly record struct CapabilityInvocationId : ITypedId
{
    /// <summary>
    /// Creates a capability invocation ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public CapabilityInvocationId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(CapabilityInvocationId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
