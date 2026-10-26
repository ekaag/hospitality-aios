using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Capabilities.Execution;

/// <summary>
/// Runs capabilities: the only way APIs, AI agents, and workflows act through the capability layer (P7). Scoped; the
/// current scope's tenant and <c>OperationContext</c> say who is acting and in which flow.
/// </summary>
public interface ICapabilityExecutor
{
    /// <summary>
    /// Looks the capability up, authorizes the caller, enforces confirmation and idempotency, validates the input, runs
    /// it, validates the output, and saves its changes with an invocation record, its events, and an audit record, all
    /// or nothing. Every attempt is audited, including rejected ones (spec §35).
    /// </summary>
    /// <returns>
    /// The output, or an error: a <see cref="CapabilityErrors"/> code for rejections, the capability's own error when it
    /// fails, or a persistence conflict when its changes could not be saved (retry with the same idempotency key).
    /// </returns>
    Task<Result<CapabilityResponse>> ExecuteAsync(CapabilityRequest request, CancellationToken cancellationToken);
}
