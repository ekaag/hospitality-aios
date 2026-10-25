namespace HospitalityOS.SharedKernel.Ids;

/// <summary>
/// Identifies a space (a physical location within a property). Lives in SharedKernel because dining,
/// operations, and guest domains reference locations, and domain projects may not reference each other (P9).
/// </summary>
public readonly record struct SpaceId : ITypedId
{
    /// <summary>
    /// Creates a space ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public SpaceId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(SpaceId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
