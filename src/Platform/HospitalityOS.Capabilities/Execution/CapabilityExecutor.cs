using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HospitalityOS.Application.Auditing;
using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Capabilities.Registry;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Domain.Capabilities.CapabilityInvocations;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Ids;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Capabilities.Execution;

/// <summary>
/// The capability pipeline (P7, P8): lookup → authorize → confirmation → idempotency key → validate input → replay a
/// key already used → run → validate output → save the changes with the invocation record, its events, and the audit
/// record in one commit. Every step that refuses records an audit entry in a scope of its own, so rejected attempts
/// are audited too (spec §35) without committing anything of the caller's.
/// </summary>
/// <remarks>
/// Authorization comes before reading the input, so callers who may not invoke a capability learn nothing about its
/// input from validation errors. Only succeeded invocations hold their key: a failed attempt can be retried with the
/// same key. Two attempts racing with one key both run, but the unique key lets only one commit; the loser discards its
/// changes and returns the winner's result.
/// </remarks>
internal sealed class CapabilityExecutor(
    CapabilityRegistry registry,
    ICurrentTenant currentTenant,
    OperationContext operation,
    Func<CapabilityScope> scopes,
    IBindingResolver<IIdGenerator> idGenerators,
    TimeProvider timeProvider) : ICapabilityExecutor
{
    private const string InvocationTarget = "CapabilityInvocation";
    private const string UnknownCapabilityAction = "capability.unknown";

    public async Task<Result<CapabilityResponse>> ExecuteAsync(CapabilityRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Name);

        CapabilityDefinition? definition = registry.Find(request.Name, request.Version);

        if (definition is null)
        {
            return await RejectAsync(ActionFor(request.Name), AuditOutcome.Denied, CapabilityErrors.Unknown(request.Name, request.Version), null, cancellationToken).ConfigureAwait(false);
        }

        if (CapabilityCallers.Of(operation.Actor) is not { } caller || !definition.Authorization.Allows(caller))
        {
            return await RejectAsync(definition.Name, AuditOutcome.Denied, CapabilityErrors.Forbidden(definition.Name), Detail(definition), cancellationToken).ConfigureAwait(false);
        }

        if (definition.ConfirmationPolicy is ConfirmationPolicy.Required && !request.Confirmed)
        {
            return await RejectAsync(definition.Name, AuditOutcome.Denied, CapabilityErrors.ConfirmationRequired(definition.Name), Detail(definition), cancellationToken).ConfigureAwait(false);
        }

        string? key = null;

        if (definition.Idempotency is IdempotencyPolicy.KeyRequired)
        {
            key = request.IdempotencyKey?.Trim();

            if (string.IsNullOrEmpty(key) || key.Length > CapabilityInvocation.MaxIdempotencyKeyLength)
            {
                return await RejectAsync(definition.Name, AuditOutcome.Failed, CapabilityErrors.IdempotencyKeyRequired(definition.Name), Detail(definition), cancellationToken).ConfigureAwait(false);
            }
        }

        object input;
        string canonicalInput;

        try
        {
            if (request.Input.ValueKind is JsonValueKind.Undefined)
            {
                throw new JsonException("No input was given.");
            }

            input = request.Input.Deserialize(definition.InputType, CapabilityJson.Options) ?? throw new JsonException("The input is null.");
            canonicalInput = JsonSerializer.Serialize(input, definition.InputType, CapabilityJson.Options);
        }
        catch (JsonException exception)
        {
            Error invalid = CapabilityErrors.InvalidInput(exception.Message);

            return await RejectAsync(definition.Name, AuditOutcome.Failed, invalid, Detail(definition, error: invalid), cancellationToken).ConfigureAwait(false);
        }

        Attempt attempt = new(
            definition,
            new CapabilityInvocationId(idGenerators.Resolve().NewId()),
            key,
            Hash(canonicalInput),
            canonicalInput,
            currentTenant.TenantId,
            operation.Actor,
            operation.CorrelationId,
            operation.CausationId);

        if (key is not null && await ReplayAsync(attempt, cancellationToken).ConfigureAwait(false) is { } replayed)
        {
            return replayed;
        }

        return await RunAsync(attempt, input, cancellationToken).ConfigureAwait(false);
    }

    private static string ActionFor(string requestedName) =>
        string.IsNullOrWhiteSpace(requestedName) ? UnknownCapabilityAction
            : requestedName.Length > AuditRecord.MaxActionLength ? requestedName[..AuditRecord.MaxActionLength]
            : requestedName;

    /// <summary>
    /// The audit detail. Inputs of sensitive capabilities, and their error messages (which may quote the input), are
    /// left out (P10).
    /// </summary>
    private static string Detail(CapabilityDefinition definition, string? canonicalInput = null, Error? error = null, Guid? replayOf = null)
    {
        bool routine = definition.AuditClassification is AuditClassification.Routine;
        List<string> parts = ["v" + definition.Version.ToString(CultureInfo.InvariantCulture)];

        if (replayOf is { } original)
        {
            parts.Add("replayed invocation " + original);
        }

        if (error is not null)
        {
            parts.Add(routine ? $"error {error.Code}: {error.Message}" : "error " + error.Code);
        }

        if (routine && canonicalInput is not null)
        {
            parts.Add("input " + canonicalInput);
        }

        return string.Join("; ", parts);
    }

    private static string Hash(string canonicalInput) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalInput)));

    private static AuditTarget TargetOf(CapabilityInvocationId id) => new(InvocationTarget, id.ToString());

    /// <summary>
    /// Commits a scope holding only records of the attempt. Such a commit cannot conflict (new IDs, no unique key), so a
    /// failure is an infrastructure fault.
    /// </summary>
    private static async Task CommitRecordAsync(CapabilityScope scope, CancellationToken cancellationToken)
    {
        Result<CommitReceipt> commit = await scope.UnitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        if (commit.IsFailure)
        {
            throw new InvalidOperationException($"Could not record a capability attempt: {commit.Error.Message}");
        }
    }

    private CapabilityScope OpenScope(Attempt attempt) =>
        scopes().Enter(attempt.TenantId, attempt.CorrelationId, attempt.CausationId, attempt.Actor);

    private async Task<Result<CapabilityResponse>> RejectAsync(string action, AuditOutcome outcome, Error error, string? detail, CancellationToken cancellationToken)
    {
        await using CapabilityScope scope = scopes().Enter(currentTenant.TenantId, operation.CorrelationId, operation.CausationId, operation.Actor);
        scope.Audit.Record(action, outcome, detail: detail ?? "error " + error.Code);
        await CommitRecordAsync(scope, cancellationToken).ConfigureAwait(false);

        return error;
    }

    /// <summary>
    /// The earlier result for the attempt's key, or <see langword="null"/> when the key is unused.
    /// </summary>
    private async Task<Result<CapabilityResponse>?> ReplayAsync(Attempt attempt, CancellationToken cancellationToken)
    {
        await using CapabilityScope scope = OpenScope(attempt);
        CapabilityInvocation? original = await scope.Invocations.FindSucceededAsync(attempt.Definition.Name, attempt.Key!, cancellationToken).ConfigureAwait(false);

        if (original is null)
        {
            return null;
        }

        CapabilityDefinition definition = attempt.Definition;

        if (original.InputHash != attempt.InputHash)
        {
            Error reused = CapabilityErrors.IdempotencyKeyReused(definition.Name);
            scope.Audit.Record(definition.Name, AuditOutcome.Failed, TargetOf(original.Id), Detail(definition, attempt.CanonicalInput, reused));
            await CommitRecordAsync(scope, cancellationToken).ConfigureAwait(false);

            return reused;
        }

        scope.Audit.Record(definition.Name, AuditOutcome.Succeeded, TargetOf(original.Id), Detail(definition, attempt.CanonicalInput, replayOf: original.Id.Value));
        await CommitRecordAsync(scope, cancellationToken).ConfigureAwait(false);

        using JsonDocument output = JsonDocument.Parse(original.Output!);

        return new CapabilityResponse(original.Id.Value, original.CapabilityName, original.CapabilityVersion, output.RootElement.Clone(), Replayed: true);
    }

    private async Task<Result<CapabilityResponse>> RunAsync(Attempt attempt, object input, CancellationToken cancellationToken)
    {
        CapabilityDefinition definition = attempt.Definition;
        string output;
        CapabilityInvocation invocation;
        Result<CommitReceipt> commit;

        await using (CapabilityScope scope = OpenScope(attempt))
        {
            invocation = Start(attempt);
            await scope.Invocations.AddAsync(invocation, cancellationToken).ConfigureAwait(false);
            Result<object?> result;

            try
            {
                result = await scope.Invoker(definition).InvokeAsync(input, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await RecordFailureAsync(attempt, CapabilityErrors.Crashed(exception), cancellationToken).ConfigureAwait(false);

                throw;
            }

            if (result.IsFailure)
            {
                return await RecordFailureAsync(attempt, result.Error, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                output = JsonSerializer.Serialize(result.Value ?? throw new JsonException("The output is null."), definition.OutputType, CapabilityJson.Options);
            }
            catch (JsonException exception)
            {
                return await RecordFailureAsync(attempt, CapabilityErrors.InvalidOutput(exception.Message), cancellationToken).ConfigureAwait(false);
            }

            invocation.Succeed(output, timeProvider.GetUtcNow());
            scope.Audit.Record(definition.Name, AuditOutcome.Succeeded, TargetOf(attempt.InvocationId), Detail(definition, attempt.CanonicalInput));
            commit = await scope.UnitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (commit.IsSuccess)
        {
            using JsonDocument written = JsonDocument.Parse(output);

            return new CapabilityResponse(invocation.Id.Value, definition.Name, definition.Version, written.RootElement.Clone(), Replayed: false);
        }

        // A racing attempt with the same key committed first: return its result; this attempt's changes are discarded.
        if (attempt.Key is not null
            && commit.Error.Code == PersistenceErrors.DuplicateKeyCode
            && await ReplayAsync(attempt, cancellationToken).ConfigureAwait(false) is { } winner)
        {
            return winner;
        }

        return await RecordFailureAsync(attempt, commit.Error, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Records the failed invocation and its audit in a fresh scope, so nothing the failed attempt changed is saved.
    /// </summary>
    private async Task<Result<CapabilityResponse>> RecordFailureAsync(Attempt attempt, Error error, CancellationToken cancellationToken)
    {
        await using CapabilityScope scope = OpenScope(attempt);
        CapabilityInvocation invocation = Start(attempt);
        invocation.Fail(error, timeProvider.GetUtcNow());
        await scope.Invocations.AddAsync(invocation, cancellationToken).ConfigureAwait(false);
        scope.Audit.Record(attempt.Definition.Name, AuditOutcome.Failed, TargetOf(attempt.InvocationId), Detail(attempt.Definition, attempt.CanonicalInput, error));
        await CommitRecordAsync(scope, cancellationToken).ConfigureAwait(false);

        return error;
    }

    private CapabilityInvocation Start(Attempt attempt) =>
        CapabilityInvocation.Request(
            attempt.InvocationId,
            attempt.TenantId,
            attempt.Definition.Name,
            attempt.Definition.Version,
            attempt.Key,
            attempt.InputHash,
            attempt.Actor,
            attempt.CorrelationId,
            attempt.CausationId,
            timeProvider.GetUtcNow());

    private sealed record Attempt(
        CapabilityDefinition Definition,
        CapabilityInvocationId InvocationId,
        string? Key,
        string InputHash,
        string CanonicalInput,
        TenantId TenantId,
        Actor Actor,
        Guid CorrelationId,
        Guid? CausationId);
}
