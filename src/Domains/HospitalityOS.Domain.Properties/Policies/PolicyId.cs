using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Properties.Policies;

/// <summary>
/// Identifies a <see cref="Policy"/>.
/// </summary>
public readonly record struct PolicyId : ITypedId
{
    /// <summary>
    /// Creates a policy ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public PolicyId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(PolicyId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
