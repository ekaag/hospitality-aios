using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Ports.Auditing;

/// <summary>
/// An immutable record that an actor attempted an action, and how it ended (P10: append-only audit). The event store
/// already records every state change; audit records cover what events do not: capability invocations, administrative
/// actions, access to sensitive data, and denied or failed attempts.
/// </summary>
/// <param name="AuditId">Unique ID (GUID v7).</param>
/// <param name="TenantId">The tenant the action was performed in.</param>
/// <param name="PropertyId">The property, if the action concerns one.</param>
/// <param name="Action">What was attempted, e.g. <c>GuestService.CreateRequest</c> or <c>property.update</c>; at most <see cref="MaxActionLength"/> characters.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="Actor">Who attempted it.</param>
/// <param name="TargetType">The kind of thing acted on, e.g. <c>ServiceRequest</c>, if any; at most <see cref="MaxTargetTypeLength"/> characters.</param>
/// <param name="TargetId">The thing acted on, if any; at most <see cref="MaxTargetIdLength"/> characters.</param>
/// <param name="CorrelationId">The end-to-end flow (P11).</param>
/// <param name="CausationId">What directly caused the action, if anything.</param>
/// <param name="OccurredAt">When (UTC).</param>
/// <param name="Detail">A short, redacted explanation (never secrets or guest personal data); at most <see cref="MaxDetailLength"/> characters.</param>
public sealed record AuditRecord(
    Guid AuditId,
    TenantId TenantId,
    PropertyId? PropertyId,
    string Action,
    AuditOutcome Outcome,
    Actor Actor,
    string? TargetType,
    string? TargetId,
    Guid CorrelationId,
    Guid? CausationId,
    DateTimeOffset OccurredAt,
    string? Detail)
{
    /// <summary>
    /// Longest <see cref="Action"/>.
    /// </summary>
    public const int MaxActionLength = 200;

    /// <summary>
    /// Longest <see cref="TargetType"/>.
    /// </summary>
    public const int MaxTargetTypeLength = 100;

    /// <summary>
    /// Longest <see cref="TargetId"/>.
    /// </summary>
    public const int MaxTargetIdLength = 200;

    /// <summary>
    /// Longest <see cref="Detail"/>.
    /// </summary>
    public const int MaxDetailLength = 2000;

    /// <summary>
    /// Longest actor identifier.
    /// </summary>
    public const int MaxActorIdLength = 200;
}
