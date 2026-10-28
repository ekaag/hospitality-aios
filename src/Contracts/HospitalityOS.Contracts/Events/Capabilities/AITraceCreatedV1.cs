using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Capabilities;

/// <summary>
/// Version 1 of <c>AITraceCreated</c>: an AI turn was recorded. Carries no guest text or model output, which may hold guest
/// data (P10); read the trace through <c>/api/v1/ai/traces</c>.
/// </summary>
/// <param name="AITraceId">The trace.</param>
/// <param name="PropertyId">The property the turn concerned, if any.</param>
/// <param name="Agent">The agent that ran it, e.g. <c>concierge.answer</c>.</param>
/// <param name="TriggerType">What kind of thing triggered it, e.g. <c>GuestMessage</c>.</param>
/// <param name="TriggerId">The message or event that triggered it, if it has an ID.</param>
/// <param name="Outcome">How it ended: <c>Answered</c>, <c>NoAuthoritativeSource</c>, or <c>Failed</c>.</param>
public sealed record AITraceCreatedV1(
    Guid AITraceId,
    PropertyId? PropertyId,
    string? Agent,
    string TriggerType,
    Guid? TriggerId,
    string Outcome) : IEventPayload;
