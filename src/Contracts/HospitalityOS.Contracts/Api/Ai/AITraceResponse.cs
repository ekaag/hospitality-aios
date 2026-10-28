using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Ai;

/// <summary>
/// One AI turn as its reader may see it (spec §26): what the reader's role may not see is withheld or masked.
/// </summary>
/// <param name="Id">The trace.</param>
/// <param name="PropertyId">The property the turn concerned, if any.</param>
/// <param name="Agent">The agent that ran it, e.g. <c>concierge.answer</c>.</param>
/// <param name="CorrelationId">The flow it ran in.</param>
/// <param name="CausationId">The event or message that directly caused it, if any.</param>
/// <param name="TriggerType">What kind of thing triggered it, e.g. <c>GuestMessage</c>.</param>
/// <param name="TriggerId">The message or event that triggered it, if it has an ID.</param>
/// <param name="Intent">What the agent understood was being asked for, if it decided.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="StartedAt">When it began.</param>
/// <param name="CompletedAt">When it was completed.</param>
/// <param name="Body">What happened.</param>
/// <param name="FlowEvents">The events recorded in the same flow, oldest first (at most
/// <see cref="MaxFlowEvents"/>), including the trace's own <c>AITraceCreated</c>.</param>
/// <param name="Redacted">Whether anything was withheld or masked for this reader.</param>
public sealed record AITraceResponse(
    Guid Id,
    PropertyId? PropertyId,
    string? Agent,
    Guid CorrelationId,
    Guid? CausationId,
    string TriggerType,
    Guid? TriggerId,
    string? Intent,
    AITraceOutcome Outcome,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    AITraceBody Body,
    ValueList<FlowEvent> FlowEvents,
    bool Redacted)
{
    /// <summary>
    /// The most flow events a trace lists.
    /// </summary>
    public const int MaxFlowEvents = 100;
}

/// <summary>
/// An event recorded in a trace's flow.
/// </summary>
/// <param name="EventId">The event.</param>
/// <param name="EventType">Its type, e.g. <c>AITraceCreated</c>.</param>
/// <param name="EventVersion">Its payload version.</param>
/// <param name="AggregateType">The kind of record that raised it.</param>
/// <param name="AggregateId">The record that raised it.</param>
/// <param name="RecordedAt">When it was recorded.</param>
public sealed record FlowEvent(Guid EventId, string EventType, int EventVersion, string AggregateType, Guid AggregateId, DateTimeOffset RecordedAt);

/// <summary>
/// One AI turn in a list.
/// </summary>
/// <param name="Id">The trace; read it at <c>/api/v1/ai/traces/{id}</c>.</param>
/// <param name="PropertyId">The property the turn concerned, if any.</param>
/// <param name="Agent">The agent that ran it.</param>
/// <param name="CorrelationId">The flow it ran in.</param>
/// <param name="TriggerType">What kind of thing triggered it.</param>
/// <param name="TriggerId">The message or event that triggered it, if it has an ID.</param>
/// <param name="Intent">What the agent understood was being asked for, if it decided.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="StartedAt">When it began.</param>
/// <param name="CompletedAt">When it was completed.</param>
public sealed record AITraceSummary(
    Guid Id,
    PropertyId? PropertyId,
    string? Agent,
    Guid CorrelationId,
    string TriggerType,
    Guid? TriggerId,
    string? Intent,
    AITraceOutcome Outcome,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);

/// <summary>
/// A page of traces, newest first.
/// </summary>
/// <param name="Traces">The traces.</param>
/// <param name="Next">Pass as <c>after</c> to read the next page; <c>null</c> on the last page.</param>
public sealed record AITracePage(ValueList<AITraceSummary> Traces, string? Next);
