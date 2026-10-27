namespace HospitalityOS.Contracts.Capabilities;

/// <summary>
/// The kind of thing a record is about.
/// </summary>
public enum SubjectKind
{
    /// <summary>
    /// The property as a whole.
    /// </summary>
    Property,

    /// <summary>
    /// A space, e.g. a floor or the lobby.
    /// </summary>
    Space,

    /// <summary>
    /// An amenity, e.g. the pool.
    /// </summary>
    Amenity,

    /// <summary>
    /// A restaurant or bar.
    /// </summary>
    Restaurant,

    /// <summary>
    /// A guest service, e.g. housekeeping.
    /// </summary>
    Service,
}
