using System.Text.Json;

namespace HospitalityOS.Capabilities.Execution;

/// <summary>
/// The result of a successful invocation.
/// </summary>
/// <param name="InvocationId">The invocation that produced the output; for a replay, the original one.</param>
/// <param name="Name">The capability name.</param>
/// <param name="Version">The capability version that ran.</param>
/// <param name="Output">The output in the wire format.</param>
/// <param name="Replayed">Whether this repeats an earlier invocation's result for the same idempotency key, without
/// running again.</param>
public sealed record CapabilityResponse(Guid InvocationId, string Name, int Version, JsonElement Output, bool Replayed);
