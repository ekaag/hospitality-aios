using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Dining.Restaurants;

/// <summary>
/// Identifies a <see cref="Restaurant"/>.
/// </summary>
public readonly record struct RestaurantId : ITypedId
{
    /// <summary>
    /// Creates a restaurant ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public RestaurantId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(RestaurantId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
