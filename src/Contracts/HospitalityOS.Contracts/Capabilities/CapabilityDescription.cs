using System.Text.Json;

namespace HospitalityOS.Contracts.Capabilities;

/// <summary>
/// A registered capability as <c>/api/v1/capabilities</c> publishes it: every spec §11 field, with the input and
/// output as JSON Schema (draft 2020-12) in the wire format.
/// </summary>
/// <param name="Name">The capability name, <c>&lt;Area&gt;.&lt;Action&gt;</c>, e.g. <c>GuestService.CreateRequest</c>.</param>
/// <param name="Version">The version, from 1; a breaking change to input or output adds a new version.</param>
/// <param name="Description">What it does and when to use it; AI agents read this to choose tools.</param>
/// <param name="InputSchema">JSON Schema of the input object.</param>
/// <param name="OutputSchema">JSON Schema of the output object.</param>
/// <param name="Authorization">Who may invoke it.</param>
/// <param name="ConfirmationPolicy">Whether it runs only after explicit confirmation.</param>
/// <param name="SideEffects">The most far-reaching effect it can have.</param>
/// <param name="Idempotency">How a repeated invocation is kept from repeating its effect.</param>
/// <param name="AuditClassification">How much of an invocation its audit record keeps.</param>
public sealed record CapabilityDescription(
    string Name,
    int Version,
    string Description,
    JsonElement InputSchema,
    JsonElement OutputSchema,
    CapabilityAuthorization Authorization,
    ConfirmationPolicy ConfirmationPolicy,
    SideEffect SideEffects,
    IdempotencyPolicy Idempotency,
    AuditClassification AuditClassification);
