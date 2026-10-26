namespace HospitalityOS.SharedKernel.Execution;

/// <summary>
/// Who performs an operation: a guest, a staff member, an AI agent or controller, or the platform itself.
/// </summary>
/// <param name="Type">The kind of actor.</param>
/// <param name="Id">The actor's identifier (staff member or guest ID, or agent name), if known.</param>
/// <param name="Role">A staff member's role; <see langword="null"/> for every other kind of actor, and for staff whose
/// role is unknown (who are then allowed nothing that needs one).</param>
public sealed record Actor(ActorType Type, string? Id = null, StaffRole? Role = null)
{
    /// <summary>
    /// The platform acting on its own, e.g. a scheduled job or a request before authentication.
    /// </summary>
    public static Actor System { get; } = new(ActorType.System);

    /// <summary>
    /// An autonomous agent or controller, by name (P7).
    /// </summary>
    public static Actor Agent(string name) => new(ActorType.Agent, name);

    /// <summary>
    /// A staff member in <paramref name="role"/>.
    /// </summary>
    public static Actor Staff(string id, StaffRole role) => new(ActorType.Staff, id, role);
}
