namespace HospitalityOS.Contracts.Capabilities;

/// <summary>
/// Who may invoke a capability (spec §11 <c>authorization</c>). Staff callers are distinguished by role
/// (Admin, Manager, Worker; plan §3), so a definition can, for example, let workers complete tasks but only
/// administrators change configuration.
/// </summary>
public enum CapabilityCaller
{
    /// <summary>
    /// A platform process with no human or agent behind it, e.g. a workflow reacting to an event.
    /// </summary>
    System,

    /// <summary>
    /// An AI agent or controller (P7). It acts only through the capabilities on its allow-list.
    /// </summary>
    Agent,

    /// <summary>
    /// A guest, through a guest channel, limited to their own stay and conversations.
    /// </summary>
    Guest,

    /// <summary>
    /// A staff member who performs physical work (HumanActions).
    /// </summary>
    Worker,

    /// <summary>
    /// A staff member who supervises operations at a property.
    /// </summary>
    Manager,

    /// <summary>
    /// A staff member who configures properties.
    /// </summary>
    Admin,
}
