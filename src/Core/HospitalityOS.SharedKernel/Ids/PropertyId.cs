namespace HospitalityOS.SharedKernel.Ids;

/// <summary>
/// Identifies a property (one hotel). Lives in SharedKernel, not its domain project, because nearly every
/// domain and the event envelope reference it and domain projects may not reference each other (P9).
/// </summary>
public readonly record struct PropertyId : ITypedId
{
    /// <summary>
    /// Creates a property ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public PropertyId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(PropertyId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
