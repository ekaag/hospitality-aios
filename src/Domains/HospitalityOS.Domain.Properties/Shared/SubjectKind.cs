namespace HospitalityOS.Domain.Properties.Shared;

/// <summary>
/// What kind of thing a <see cref="SubjectReference"/> points at.
/// </summary>
public enum SubjectKind
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
    /// A service, e.g. room service (Services domain).
    /// </summary>
    Service,
}
