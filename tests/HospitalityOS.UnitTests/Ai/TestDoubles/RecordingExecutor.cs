using System.Collections.Concurrent;
using System.Text.Json;
using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Ports.Execution;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.UnitTests.Ai.TestDoubles;

/// <summary>
/// What the capability executor was asked, by whom, and what it answers: scripted results in order, or by default the
/// arguments echoed as <c>{"echo": ...}</c>.
/// </summary>
internal sealed class ExecutorScript
{
    public ConcurrentQueue<Result<JsonElement>> Results { get; } = new();

    public List<(CapabilityRequest Request, Actor Actor)> Calls { get; } = [];
}

/// <summary>
/// Stands in for the capability executor, which has its own tests: records each request with the scope's actor.
/// </summary>
internal sealed class RecordingExecutor(ExecutorScript script, OperationContext operation) : ICapabilityExecutor
{
    public Task<Result<CapabilityResponse>> ExecuteAsync(CapabilityRequest request, CancellationToken cancellationToken)
    {
        script.Calls.Add((request, operation.Actor));
        Result<JsonElement> output = script.Results.TryDequeue(out Result<JsonElement>? scripted)
            ? scripted
            : JsonSerializer.SerializeToElement(new { echo = request.Input });

        return Task.FromResult(output.IsSuccess
            ? Result.Success(new CapabilityResponse(InvocationFor(script.Calls.Count), request.Name, request.Version ?? 0, output.Value, false))
            : Result.Failure<CapabilityResponse>(output.Error));
    }

    /// <summary>
    /// The invocation ID the <paramref name="call"/>-th call (from 1) reports.
    /// </summary>
    public static Guid InvocationFor(int call) => new($"01890a5d-ac96-774b-bcce-{call:D12}");
}
