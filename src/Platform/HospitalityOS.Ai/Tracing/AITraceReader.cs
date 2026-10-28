using System.Globalization;
using System.Text.Json;
using HospitalityOS.Contracts;
using HospitalityOS.Contracts.Api.Ai;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Domain.Capabilities.AITraces;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Results;
using Microsoft.Extensions.Options;
using ContractOutcome = HospitalityOS.Contracts.Api.Ai.AITraceOutcome;
using DomainOutcome = HospitalityOS.Domain.Capabilities.AITraces.AITraceOutcome;

namespace HospitalityOS.Ai.Tracing;

/// <summary>
/// Reads traces for staff (see <see cref="IAITraceReader"/>).
/// </summary>
internal sealed class AITraceReader(
    IAITraceRepository traces,
    IEventStore events,
    ICurrentTenant currentTenant,
    OperationContext operation,
    IOptions<AITraceRedactionOptions> redaction) : IAITraceReader
{
    public const int DefaultLimit = 50;

    public async Task<Result<AITraceResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        AITrace? trace = id == Guid.Empty ? null : await traces.GetByIdAsync(new AITraceId(id), cancellationToken).ConfigureAwait(false);

        if (trace is null)
        {
            return Error.NotFound(AITraceErrors.NotFoundCode, $"AI trace {id} was not found.");
        }

        AITraceBody body = JsonSerializer.Deserialize<AITraceBody>(trace.Body, ContractJson.Strict)
            ?? throw new InvalidOperationException($"AI trace {trace.Id} has no body.");
        (AITraceBody shown, bool redacted) = redaction.Value.Reveals(operation.Actor.Role) ? (body, false) : AITraceRedactor.Redact(body);

        IReadOnlyList<StoredEvent> flow = await events
            .ReadAsync(new EventStoreQuery(Limit: AITraceResponse.MaxFlowEvents, TenantId: currentTenant.TenantId, CorrelationId: trace.CorrelationId), cancellationToken)
            .ConfigureAwait(false);

        return new AITraceResponse(
            trace.Id.Value,
            trace.PropertyId,
            trace.ActorId,
            trace.CorrelationId,
            trace.CausationId,
            trace.TriggerType,
            trace.TriggerId,
            trace.Intent,
            EnumMirror.To<ContractOutcome>(trace.Outcome),
            trace.StartedAt,
            trace.CompletedAt,
            shown,
            ValueList.From(flow.Select(stored => new FlowEvent(
                stored.Message.EventId,
                stored.Message.EventType,
                stored.Message.EventVersion,
                stored.Message.AggregateType,
                stored.Message.AggregateId,
                stored.Message.RecordedAt))),
            redacted);
    }

    public async Task<Result<AITracePage>> ListAsync(AITraceListRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<FieldError> errors = [];
        int limit = request.Limit ?? DefaultLimit;

        if (limit is < 1 or > AITraceQuery.MaxLimit)
        {
            errors.Add(new FieldError("limit", "limit.out_of_range", $"The limit must be from 1 to {AITraceQuery.MaxLimit}."));
        }

        AITraceCursor? after = null;

        if (request.After is { } token)
        {
            after = ParseCursor(token);

            if (after is null)
            {
                errors.Add(new FieldError("after", "after.invalid", "The cursor is not one this API returned."));
            }
        }

        if (errors.Count > 0)
        {
            return ValidationError.From(errors);
        }

        IReadOnlyList<AITrace> page = await traces.ListAsync(
            new AITraceQuery(
                limit,
                after,
                request.PropertyId,
                request.CorrelationId,
                request.Outcome is { } outcome ? EnumMirror.To<DomainOutcome>(outcome) : null,
                request.CompletedFrom,
                request.CompletedBefore),
            cancellationToken).ConfigureAwait(false);

        string? next = page.Count == limit ? FormatCursor(new AITraceCursor(page[^1].CompletedAt, page[^1].Id)) : null;

        return new AITracePage(ValueList.From(page.Select(Summary)), next);
    }

    private static AITraceSummary Summary(AITrace trace) => new(
        trace.Id.Value,
        trace.PropertyId,
        trace.ActorId,
        trace.CorrelationId,
        trace.TriggerType,
        trace.TriggerId,
        trace.Intent,
        EnumMirror.To<ContractOutcome>(trace.Outcome),
        trace.StartedAt,
        trace.CompletedAt);

    private static string FormatCursor(AITraceCursor cursor) =>
        string.Create(CultureInfo.InvariantCulture, $"{cursor.CompletedAt.UtcTicks}.{cursor.Id.Value:N}");

    private static AITraceCursor? ParseCursor(string token)
    {
        string[] parts = token.Split('.');

        return parts.Length == 2
            && long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out long ticks)
            && ticks <= DateTimeOffset.MaxValue.UtcTicks
            && Guid.TryParseExact(parts[1], "N", out Guid id)
            && id != Guid.Empty
                ? new AITraceCursor(new DateTimeOffset(ticks, TimeSpan.Zero), new AITraceId(id))
                : null;
    }
}
