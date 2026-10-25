using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Activities.PropertyEvents;

/// <summary>
/// Identifies a <see cref="PropertyEvent"/>.
/// </summary>
public readonly record struct PropertyEventId : ITypedId
{
    /// <summary>
    /// Creates a property event ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public PropertyEventId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(PropertyEventId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
