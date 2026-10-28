using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Ai.Grounding;

/// <summary>
/// Answers a guest's question only from authoritative property information (P4, spec §16, §28). It builds the context
/// first, structured before searched: the property profile (<c>Property.GetProperty</c>: name, local time, amenities and
/// restaurants with their IDs), then the published content that best matches the question (<c>Property.SearchContent</c>),
/// each read through the capability executor as the agent and each carrying its source. It then runs the task's tool
/// loop for precise reads, asking for a structured reply: answered with citations, or unknown. Every citation must be a
/// source the context or a tool returned in this turn. An answer that is not grounded is sent back once with the reason;
/// if it is still not grounded, or the model says the sources do not answer the question, the outcome is
/// <see cref="GroundedOutcome.NoAuthoritativeSource"/> and the guest gets the caller's honest answer, never the model's
/// unsupported claim. Scoped: run it in the operation it serves, which then acts as the agent.
/// </summary>
public interface IGroundedAnswerer
{
    /// <summary>
    /// Answers the last guest message of <paramref name="question"/>.
    /// </summary>
    /// <returns>The answer or the no-authoritative-source outcome, with everything that led to it; or an error when the
    /// property is unknown, or the model could not be reached (<c>inference.unavailable</c>,
    /// <c>inference.tool_round_limit</c>).</returns>
    /// <exception cref="InvalidOperationException">The task is not declared with <c>AddGroundedAnswering</c>.</exception>
    /// <exception cref="ArgumentException">There is no guest message, or the caller supplies the <c>context</c> prompt value.</exception>
    Task<Result<GroundedAnswer>> AnswerAsync(GroundedQuestion question, CancellationToken cancellationToken);
}
