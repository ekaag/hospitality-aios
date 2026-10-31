namespace HospitalityOS.Contracts.Capabilities.HumanActions;

/// <summary>
/// The kind of physical work (spec §7 minimum action types).
/// </summary>
public enum ActionType
{
    /// <summary>
    /// Bring items to a location, e.g. towels to Room 412.
    /// </summary>
    DeliverItems,

    /// <summary>
    /// Clean a room.
    /// </summary>
    CleanRoom,

    /// <summary>
    /// Inspect a room's condition.
    /// </summary>
    InspectRoom,

    /// <summary>
    /// Check a reported issue in a room.
    /// </summary>
    CheckRoom,

    /// <summary>
    /// Repair equipment, e.g. the AC.
    /// </summary>
    RepairEquipment,

    /// <summary>
    /// Restock an item.
    /// </summary>
    RestockItem,

    /// <summary>
    /// Anything else.
    /// </summary>
    Other,
}
