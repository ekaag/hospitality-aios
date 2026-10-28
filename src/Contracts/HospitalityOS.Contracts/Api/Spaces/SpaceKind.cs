namespace HospitalityOS.Contracts.Api.Spaces;

/// <summary>
/// What kind of physical place a space is. Any kind may contain any other.
/// </summary>
public enum SpaceKind
{
    /// <summary>
    /// A building, e.g. Main Building.
    /// </summary>
    Building,

    /// <summary>
    /// A wing or tower within a building, e.g. East Wing.
    /// </summary>
    Wing,

    /// <summary>
    /// A floor or level, e.g. Floor 4.
    /// </summary>
    Floor,

    /// <summary>
    /// An enclosed room, e.g. Room 412, Gym, Ballroom.
    /// </summary>
    Room,

    /// <summary>
    /// An indoor area that is not a room, e.g. Lobby, Pool Deck.
    /// </summary>
    Area,

    /// <summary>
    /// An outdoor place, e.g. Garden, Parking.
    /// </summary>
    Outdoor,
}
