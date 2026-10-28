using System.Text.Json;
using HospitalityOS.Contracts.Capabilities;

namespace HospitalityOS.Contracts.Api.Ai;

/// <summary>
/// One capability call in an AI turn: what ran, whether it was allowed, and what it returned.
/// </summary>
/// <param name="Purpose">Read as context, or called by the model as a tool.</param>
/// <param name="Capability">The capability and version that ran, e.g. <c>Property.GetOperatingHours@1</c>; for a tool not on
/// the allow-list, the tool name the model used.</param>
/// <param name="CallId">The model's tool call ID, for tool calls.</param>
/// <param name="Classification">The capability's audit classification, when it exists.</param>
/// <param name="Authorization">The authorization and policy decision.</param>
/// <param name="Input">The input sent; <c>null</c> when withheld.</param>
/// <param name="Output">The output returned, when it succeeded; <c>null</c> when withheld.</param>
/// <param name="InvocationId">The capability invocation, when it succeeded.</param>
/// <param name="ErrorCode">Why it failed, or <c>null</c> when it succeeded.</param>
/// <param name="Withheld">Whether the input and output were withheld from this reader (sensitive capabilities).</param>
public sealed record CapabilityCall(
    CapabilityCallPurpose Purpose,
    string Capability,
    string? CallId,
    AuditClassification? Classification,
    AuthorizationDecision Authorization,
    JsonElement? Input,
    JsonElement? Output,
    Guid? InvocationId,
    string? ErrorCode,
    bool Withheld = false);
