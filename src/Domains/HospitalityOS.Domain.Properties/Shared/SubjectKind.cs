namespace HospitalityOS.Domain.Properties.Hours;

/// <summary>
/// What kind of thing an <see cref="OperatingHours"/> record describes.
/// </summary>
public enum HoursSubjectKind
{
    /// <summary>
    /// The property itself, e.g. front desk hours.
    /// </summary>
    Property,

    /// <summary>
    /// A space, e.g. the pool deck.
    /// </summary>
    Space,

    /// <summary>
    /// An amenity, e.g. the gym.
    /// </summary>
    Amenity,

    /// <summary>
    /// A restaurant (Dining domain).
    /// </summary>
    Restaurant,

    /// <summary>
    /// A service, e.g. room service.
    /// </summary>
    Service,
}
