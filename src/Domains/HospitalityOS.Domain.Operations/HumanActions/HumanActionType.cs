namespace HospitalityOS.Domain.Operations.HumanActions;

/// <summary>
/// The kind of physical work (spec §7 minimum action types).
/// </summary>
public enum HumanActionType
{
    /// <summary>
    /// Bring items to a location, e.g. towels to Room 412 (DELIVER_ITEMS).
    /// </summary>
    DeliverItems,

    /// <summary>
    /// Clean a room (CLEAN_ROOM).
    /// </summary>
    CleanRoom,

    /// <summary>
    /// Inspect a room's condition (INSPECT_ROOM).
    /// </summary>
    InspectRoom,

    /// <summary>
    /// Check a reported issue in a room (CHECK_ROOM).
    /// </summary>
    CheckRoom,

    /// <summary>
    /// Repair equipment, e.g. the AC (REPAIR_EQUIPMENT).
    /// </summary>
    RepairEquipment,

    /// <summary>
    /// Restock an item (RESTOCK_ITEM).
    /// </summary>
    RestockItem,

    /// <summary>
    /// Anything else (OTHER).
    /// </summary>
    Other,
}
