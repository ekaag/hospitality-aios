using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Properties.Amenities;

/// <summary>
/// Identifies an <see cref="Amenity"/>.
/// </summary>
public readonly record struct AmenityId : ITypedId
{
    /// <summary>
    /// Creates an amenity ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public AmenityId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(AmenityId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
