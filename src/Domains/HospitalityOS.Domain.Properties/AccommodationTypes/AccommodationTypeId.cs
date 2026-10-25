using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Properties.AccommodationTypes;

/// <summary>
/// Identifies an <see cref="AccommodationType"/>.
/// </summary>
public readonly record struct AccommodationTypeId : ITypedId
{
    /// <summary>
    /// Creates an accommodation type ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public AccommodationTypeId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(AccommodationTypeId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
