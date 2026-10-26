namespace HospitalityOS.Contracts.Events;

/// <summary>
/// Who caused an event (spec §8 <c>actorType</c>).
/// </summary>
public enum ActorType
{
    /// <summary>
    /// A platform process with no human or agent behind it, e.g. a scheduled expiry.
    /// </summary>
    System,

    /// <summary>
    /// A guest, through a guest channel.
    /// </summary>
    Guest,

    /// <summary>
    /// A staff member: administrator, manager, or worker.
    /// </summary>
    Staff,

    /// <summary>
    /// An autonomous AI agent or controller acting through capabilities (P7).
    /// </summary>
    Agent,
}
