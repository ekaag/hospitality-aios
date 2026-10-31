namespace HospitalityOS.Contracts.Api.Work;

/// <summary>
/// What the caller may do to a piece of work now, so the surface shows only those buttons (Journey 13: the worker does
/// not see the orchestration).
/// </summary>
public enum WorkAction
{
    /// <summary>
    /// The assignee takes it on.
    /// </summary>
    Accept,

    /// <summary>
    /// The assignee starts it.
    /// </summary>
    Start,

    /// <summary>
    /// The assignee finishes it, with an optional note.
    /// </summary>
    Complete,

    /// <summary>
    /// The assignee cannot do it, and says why.
    /// </summary>
    Fail,

    /// <summary>
    /// An eligible worker takes unassigned work: it is assigned to them and accepted.
    /// </summary>
    Claim,

    /// <summary>
    /// A manager gives work nobody has accepted to someone.
    /// </summary>
    Assign,

    /// <summary>
    /// A manager asks for the work again, so its request gets new work.
    /// </summary>
    RequestAgain,
}
