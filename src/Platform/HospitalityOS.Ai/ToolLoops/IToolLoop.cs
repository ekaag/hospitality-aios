using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Ai.ToolLoops;

/// <summary>
/// Runs a conversation turn through a chat task whose model may call capabilities as tools (P7): only the capabilities
/// on the task's allow-list (declared with <c>AddToolLoop</c>), at their pinned versions, are offered, and anything else
/// the model calls is refused without running. Each call goes through <c>ICapabilityExecutor</c> as the agent
/// (<c>Actor.Agent(task)</c>), so it is authorized, validated, recorded, and audited like any other; invalid arguments
/// and capability errors go back to the model as tool errors so it can correct itself or say it does not know. Scoped:
/// run it in the scope of the operation it serves, which then acts as the agent.
/// </summary>
public interface IToolLoop
{
    /// <summary>
    /// Asks the model, runs the tools it calls, and asks again with their results, until it answers or the task's round
    /// limit is reached.
    /// </summary>
    /// <returns>The answer with everything that led to it, or an error: <c>inference.unavailable</c> when no provider
    /// could serve a round, <see cref="AiErrors.ToolRoundLimitCode"/> when the model never stopped calling tools.</returns>
    /// <exception cref="InvalidOperationException">No tool loop is declared for the task, or the scope already acts as
    /// someone else.</exception>
    Task<Result<ToolLoopResult>> RunAsync(ToolLoopRequest request, CancellationToken cancellationToken);
}
