using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Capabilities.AITraces;

/// <summary>
/// Identifies an <see cref="AITrace"/>.
/// </summary>
public readonly record struct AITraceId : ITypedId
{
    /// <summary>
    /// Creates an AI trace ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public AITraceId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(AITraceId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
