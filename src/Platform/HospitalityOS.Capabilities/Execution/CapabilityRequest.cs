using System.Text.Json;

namespace HospitalityOS.Capabilities.Execution;

/// <summary>
/// A request to invoke a capability, as an API, the AI tool loop, or a workflow makes it. Who is asking and in which
/// flow come from the caller's scope (<c>OperationContext</c> and tenant), never from the request.
/// </summary>
/// <param name="Name">The capability name, exact.</param>
/// <param name="Input">The input in the wire format.</param>
/// <param name="Version">The version to run; the latest when omitted.</param>
/// <param name="IdempotencyKey">The caller's key, for capabilities that require one: a repeated key returns the first
/// result without running again. Trimmed; at most 200 characters.</param>
/// <param name="Confirmed">Whether the person it acts for explicitly confirmed it, for capabilities that require
/// confirmation.</param>
public sealed record CapabilityRequest(string Name, JsonElement Input, int? Version = null, string? IdempotencyKey = null, bool Confirmed = false);
