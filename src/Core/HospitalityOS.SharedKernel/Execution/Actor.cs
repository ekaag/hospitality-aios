namespace HospitalityOS.SharedKernel.Execution;

/// <summary>
/// Who performs an operation: a guest, a staff member, an AI agent or controller, or the platform itself.
/// </summary>
/// <param name="Type">The kind of actor.</param>
/// <param name="Id">The actor's identifier (staff member or guest ID, or agent name), if known.</param>
public sealed record Actor(ActorType Type, string? Id = null)
{
    /// <summary>
    /// The platform acting on its own, e.g. a scheduled job or a request before authentication.
    /// </summary>
    public static Actor System { get; } = new(ActorType.System);

    /// <summary>
    /// An autonomous agent or controller, by name (P7).
    /// </summary>
    public static Actor Agent(string name) => new(ActorType.Agent, name);
}
