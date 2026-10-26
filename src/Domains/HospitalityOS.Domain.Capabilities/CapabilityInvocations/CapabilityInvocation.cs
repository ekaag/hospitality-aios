using HospitalityOS.Domain.Capabilities.CapabilityInvocations.Events;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Domain.Capabilities.CapabilityInvocations;

/// <summary>
/// One accepted invocation of a capability (spec §6): who invoked which capability version, in which flow, and how it
/// ended. A succeeded invocation keeps its output, so repeating its idempotency key returns the same result without
/// running again (P8).
/// </summary>
/// <remarks>
/// Created and completed by the capability executor in the same commit as the capability's own changes, so a
/// succeeded invocation always means its effects were saved. The input is kept only as a hash: enough to tell a
/// reused key from a repeat, without storing guest data twice (P10). Values come from the executor, which validates
/// caller-supplied ones first, so invalid values here are programming errors and throw.
/// </remarks>
public sealed class CapabilityInvocation : AggregateRoot<CapabilityInvocationId>, ITenantScoped
{
    /// <summary>
    /// Maximum length of <see cref="CapabilityName"/>.
    /// </summary>
    public const int MaxCapabilityNameLength = 64;

    /// <summary>
    /// Maximum length of <see cref="IdempotencyKey"/>.
    /// </summary>
    public const int MaxIdempotencyKeyLength = 200;

    /// <summary>
    /// Length of <see cref="InputHash"/>: SHA-256 as lower-case hex.
    /// </summary>
    public const int InputHashLength = 64;

    /// <summary>
    /// Maximum length of <see cref="ErrorCode"/>.
    /// </summary>
    public const int MaxErrorCodeLength = 200;

    /// <summary>
    /// Maximum length of <see cref="ErrorMessage"/>; longer messages are truncated.
    /// </summary>
    public const int MaxErrorMessageLength = 2000;

    /// <summary>
    /// Maximum length of <see cref="ActorId"/>.
    /// </summary>
    public const int MaxActorIdLength = 200;

    // For EF Core only: it sets every property from storage before the instance is used, so the
    // placeholders below are never read.
    private CapabilityInvocation()
    {
        CapabilityName = null!;
        InputHash = null!;
    }

    private CapabilityInvocation(
        CapabilityInvocationId id,
        TenantId tenantId,
        string capabilityName,
        int capabilityVersion,
        string? idempotencyKey,
        string inputHash,
        Actor actor,
        Guid correlationId,
        Guid? causationId,
        DateTimeOffset requestedAt)
        : base(id)
    {
        TenantId = tenantId;
        CapabilityName = capabilityName;
        CapabilityVersion = capabilityVersion;
        IdempotencyKey = idempotencyKey;
        InputHash = inputHash;
        ActorType = actor.Type;
        ActorId = actor.Id;
        ActorRole = actor.Role;
        CorrelationId = correlationId;
        CausationId = causationId;
        Status = CapabilityInvocationStatus.Requested;
        RequestedAt = requestedAt;
    }

    /// <inheritdoc />
    public TenantId TenantId { get; }

    /// <summary>
    /// The capability, <c>&lt;Area&gt;.&lt;Action&gt;</c>.
    /// </summary>
    public string CapabilityName { get; }

    /// <summary>
    /// The capability version that ran.
    /// </summary>
    public int CapabilityVersion { get; }

    /// <summary>
    /// The caller's idempotency key, for capabilities that require one.
    /// </summary>
    public string? IdempotencyKey { get; }

    /// <summary>
    /// SHA-256 of the input in the wire format, lower-case hex.
    /// </summary>
    public string InputHash { get; }

    /// <summary>
    /// The kind of actor that invoked it.
    /// </summary>
    public ActorType ActorType { get; }

    /// <summary>
    /// The actor's identifier, if known.
    /// </summary>
    public string? ActorId { get; }

    /// <summary>
    /// The staff member's role, for staff actors.
    /// </summary>
    public StaffRole? ActorRole { get; }

    /// <summary>
    /// The flow it ran in (P11).
    /// </summary>
    public Guid CorrelationId { get; }

    /// <summary>
    /// The event or message that caused it, if any.
    /// </summary>
    public Guid? CausationId { get; }

    /// <summary>
    /// Where it is.
    /// </summary>
    public CapabilityInvocationStatus Status { get; private set; }

    /// <summary>
    /// The output in the wire format, once succeeded.
    /// </summary>
    public string? Output { get; private set; }

    /// <summary>
    /// The error code, once failed.
    /// </summary>
    public string? ErrorCode { get; private set; }

    /// <summary>
    /// The error message, once failed.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// When it was accepted (UTC).
    /// </summary>
    public DateTimeOffset RequestedAt { get; }

    /// <summary>
    /// When it succeeded or failed (UTC).
    /// </summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>
    /// The actor that invoked it.
    /// </summary>
    public Actor Actor => new(ActorType, ActorId, ActorRole);

    /// <summary>
    /// Records an accepted invocation. Raises <see cref="CapabilityInvocationRequested"/>.
    /// </summary>
    /// <param name="id">New ID, generated by the caller through <c>IIdGenerator</c>.</param>
    /// <param name="tenantId">The tenant it runs for.</param>
    /// <param name="capabilityName">The capability's name.</param>
    /// <param name="capabilityVersion">The version that runs, from 1.</param>
    /// <param name="idempotencyKey">The caller's key, trimmed, if the capability requires one.</param>
    /// <param name="inputHash">SHA-256 of the input, lower-case hex.</param>
    /// <param name="actor">Who invoked it.</param>
    /// <param name="correlationId">The flow.</param>
    /// <param name="causationId">The cause, if any.</param>
    /// <param name="now">Current time from the injected <see cref="TimeProvider"/>.</param>
    public static CapabilityInvocation Request(
        CapabilityInvocationId id,
        TenantId tenantId,
        string capabilityName,
        int capabilityVersion,
        string? idempotencyKey,
        string inputHash,
        Actor actor,
        Guid correlationId,
        Guid? causationId,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityName);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(capabilityName.Length, MaxCapabilityNameLength, nameof(capabilityName));
        ArgumentOutOfRangeException.ThrowIfLessThan(capabilityVersion, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(idempotencyKey?.Length ?? 0, MaxIdempotencyKeyLength, nameof(idempotencyKey));
        ArgumentNullException.ThrowIfNull(inputHash);
        ArgumentOutOfRangeException.ThrowIfNotEqual(inputHash.Length, InputHashLength, nameof(inputHash));
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(actor.Id?.Length ?? 0, MaxActorIdLength, nameof(actor));

        if (idempotencyKey is not null && (idempotencyKey.Length == 0 || idempotencyKey != idempotencyKey.Trim()))
        {
            throw new ArgumentException("An idempotency key must be non-empty and trimmed.", nameof(idempotencyKey));
        }

        CapabilityInvocation invocation = new(id, tenantId, capabilityName, capabilityVersion, idempotencyKey, inputHash, actor, correlationId, causationId, now);
        invocation.Raise(new CapabilityInvocationRequested(id, capabilityName, capabilityVersion, now));

        return invocation;
    }

    /// <summary>
    /// Records that it ran and keeps its output. Raises <see cref="CapabilityInvocationSucceeded"/>.
    /// </summary>
    /// <param name="output">The output in the wire format.</param>
    /// <param name="now">Current time.</param>
    /// <exception cref="InvalidOperationException">It already succeeded or failed.</exception>
    public void Succeed(string output, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(output);
        EnsureRequested();

        Status = CapabilityInvocationStatus.Succeeded;
        Output = output;
        CompletedAt = now;
        Raise(new CapabilityInvocationSucceeded(Id, CapabilityName, CapabilityVersion, now));
    }

    /// <summary>
    /// Records that it failed. Raises <see cref="CapabilityInvocationFailed"/>.
    /// </summary>
    /// <param name="error">Why; an over-long message is truncated.</param>
    /// <param name="now">Current time.</param>
    /// <exception cref="InvalidOperationException">It already succeeded or failed.</exception>
    public void Fail(Error error, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(error.Code.Length, MaxErrorCodeLength, nameof(error));
        EnsureRequested();

        Status = CapabilityInvocationStatus.Failed;
        ErrorCode = error.Code;
        ErrorMessage = error.Message.Length > MaxErrorMessageLength ? error.Message[..MaxErrorMessageLength] : error.Message;
        CompletedAt = now;
        Raise(new CapabilityInvocationFailed(Id, CapabilityName, CapabilityVersion, error.Code, now));
    }

    private void EnsureRequested()
    {
        if (Status != CapabilityInvocationStatus.Requested)
        {
            throw new InvalidOperationException($"Capability invocation {Id} already {Status.ToString().ToLowerInvariant()}.");
        }
    }
}
