using HospitalityOS.Domain.Capabilities.AITraces;

namespace HospitalityOS.Ai.Tracing;

/// <summary>
/// Records the scope's AI turn as one <see cref="AITrace"/> (spec §26, P11). The tool loop, typed extraction, and grounded
/// answering add every model call (with its exact prompt, messages, and providers tried), capability call (with its
/// authorization decision, input, and output), and grounding verdict as they happen; the agent that ran the turn completes
/// it with what triggered it and what it answered. Scoped: one turn per scope.
/// </summary>
public interface IAITraceRecorder
{
    /// <summary>
    /// Stages the turn's trace, with everything recorded so far, as one <see cref="AITrace"/> raising <c>AITraceCreated</c>.
    /// It is saved when the scope's unit of work commits, together with the agent's own changes, and not at all if that
    /// commit fails; a turn that changed nothing else must still commit. Nothing can be recorded afterwards.
    /// </summary>
    /// <param name="completion">How the turn ended.</param>
    /// <param name="cancellationToken">Cancels staging.</param>
    /// <returns>The trace's ID.</returns>
    /// <exception cref="InvalidOperationException">The turn was already completed, or a citation was not retrieved during
    /// it.</exception>
    Task<AITraceId> CompleteAsync(AITraceCompletion completion, CancellationToken cancellationToken);
}
