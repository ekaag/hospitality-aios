using HospitalityOS.Contracts.Capabilities;

namespace HospitalityOS.Capabilities.Definitions;

/// <summary>
/// What a capability declares about itself (spec §11); the input and output schemas come from its types. Checked when
/// the capability is registered, so a bad declaration fails at startup.
/// </summary>
public sealed record CapabilityDescriptor
{
    /// <summary>
    /// <c>&lt;Area&gt;.&lt;Action&gt;</c> in PascalCase, e.g. <c>GuestService.CreateRequest</c>; at most
    /// <see cref="CapabilityDefinition.MaxNameLength"/> characters.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// From 1. A published version never changes; a breaking change to input or output registers a new version.
    /// </summary>
    public required int Version { get; init; }

    /// <summary>
    /// What it does and when to use it, written for the AI agents that choose it as a tool; at most
    /// <see cref="CapabilityDefinition.MaxDescriptionLength"/> characters.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Who may invoke it.
    /// </summary>
    public required CapabilityAuthorization Authorization { get; init; }

    /// <summary>
    /// Whether it runs only after explicit confirmation. Read-only capabilities never require it.
    /// </summary>
    public required ConfirmationPolicy ConfirmationPolicy { get; init; }

    /// <summary>
    /// The most far-reaching effect it can have.
    /// </summary>
    public required SideEffect SideEffects { get; init; }

    /// <summary>
    /// How a repeated invocation is kept from repeating its effect: read-only capabilities are
    /// <see cref="IdempotencyPolicy.Inherent"/>; external effects need <see cref="IdempotencyPolicy.KeyRequired"/>.
    /// </summary>
    public required IdempotencyPolicy Idempotency { get; init; }

    /// <summary>
    /// How much of an invocation its audit record keeps.
    /// </summary>
    public required AuditClassification AuditClassification { get; init; }
}
