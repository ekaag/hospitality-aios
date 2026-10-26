using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;
using HospitalityOS.SharedKernel.Execution;

namespace HospitalityOS.Ports.Execution;

/// <summary>
/// The ambient context of one operation (an HTTP request or a handled event): which end-to-end flow it belongs to,
/// what directly caused it, and who is acting (P11). Everything the operation records (events, audit records, later
/// capability invocations and AI traces) carries it, so any decision can be traced back. Scoped; hosts register it.
/// </summary>
/// <remarks>
/// Whatever starts the operation sets it first: the HTTP pipeline adopts the caller's <c>X-Correlation-Id</c>,
/// authentication sets the actor (F1), and the event processor continues the handled event's flow. If nothing sets the
/// flow, the first read starts a new one; the actor defaults to <see cref="Actor.System"/>.
/// </remarks>
public sealed class OperationContext(IBindingResolver<IIdGenerator> idGenerators)
{
    private Guid? correlationId;
    private Actor? actor;

    /// <summary>
    /// The end-to-end flow; generated (GUID v7) on first read if no one set it.
    /// </summary>
    public Guid CorrelationId => correlationId ??= idGenerators.Resolve().NewId();

    /// <summary>
    /// The event or command that directly caused this operation, if any.
    /// </summary>
    public Guid? CausationId { get; private set; }

    /// <summary>
    /// Who is acting.
    /// </summary>
    public Actor Actor => actor ?? Actor.System;

    /// <summary>
    /// Joins an existing flow. Must happen before anything reads <see cref="CorrelationId"/>: switching flows mid-operation
    /// would split its records across two traces.
    /// </summary>
    /// <param name="correlation">The flow to join.</param>
    /// <param name="causation">What caused this operation, if anything.</param>
    public void JoinFlow(Guid correlation, Guid? causation)
    {
        if (correlation == Guid.Empty)
        {
            throw new ArgumentException("A correlation ID cannot be empty.", nameof(correlation));
        }

        if (correlationId is { } current && current != correlation)
        {
            throw new InvalidOperationException($"This operation already belongs to flow {current}; it cannot join {correlation}.");
        }

        correlationId = correlation;
        CausationId = causation;
    }

    /// <summary>
    /// Sets who is acting, once.
    /// </summary>
    /// <param name="acting">The actor.</param>
    public void ActAs(Actor acting)
    {
        ArgumentNullException.ThrowIfNull(acting);

        if (actor is { } current && current != acting)
        {
            throw new InvalidOperationException($"This operation already acts as {current.Type} '{current.Id}'.");
        }

        actor = acting;
    }
}
