using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Operations.HumanActions;

/// <summary>
/// Identifies a <see cref="HumanAction"/>.
/// </summary>
public readonly record struct HumanActionId : ITypedId
{
    /// <summary>
    /// Creates a human action ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public HumanActionId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(HumanActionId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
