namespace HospitalityOS.SharedKernel.Ids;

/// <summary>
/// Identifies an accommodation unit (an occupiable room, e.g. Room 412). Lives in SharedKernel because guest
/// stays and operations reference it, and domain projects may not reference each other (P9).
/// </summary>
public readonly record struct AccommodationUnitId : ITypedId
{
    /// <summary>
    /// Creates an accommodation unit ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public AccommodationUnitId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(AccommodationUnitId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
