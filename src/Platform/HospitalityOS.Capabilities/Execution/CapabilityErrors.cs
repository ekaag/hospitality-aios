using HospitalityOS.Domain.Capabilities.CapabilityInvocations;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Capabilities.Execution;

/// <summary>
/// The executor's own expected failures, each with its own code at the top level so callers (the AI tool loop above
/// all) can act on it. A capability's own failures keep their codes.
/// </summary>
public static class CapabilityErrors
{
    /// <summary>
    /// No capability has this name (and version): unknown capabilities are rejected (P7).
    /// </summary>
    public const string UnknownCode = "capability.unknown";

    /// <summary>
    /// The caller may not invoke the capability.
    /// </summary>
    public const string ForbiddenCode = "capability.forbidden";

    /// <summary>
    /// The capability requires explicit confirmation and the request has none.
    /// </summary>
    public const string ConfirmationRequiredCode = "capability.confirmation_required";

    /// <summary>
    /// The capability requires an idempotency key and the request has none, or one that is too long.
    /// </summary>
    public const string IdempotencyKeyRequiredCode = "capability.idempotency_key_required";

    /// <summary>
    /// The idempotency key already belongs to a succeeded invocation with different input.
    /// </summary>
    public const string IdempotencyKeyReusedCode = "capability.idempotency_key_reused";

    /// <summary>
    /// The input does not match the capability's input schema.
    /// </summary>
    public const string InvalidInputCode = "capability.invalid_input";

    /// <summary>
    /// The capability produced output that does not match its output schema (a defect in the capability).
    /// </summary>
    public const string InvalidOutputCode = "capability.invalid_output";

    /// <summary>
    /// The capability threw (a defect or infrastructure fault). Recorded, then the exception is rethrown.
    /// </summary>
    public const string CrashedCode = "capability.crashed";

    internal static Error Unknown(string name, int? version) =>
        Error.NotFound(UnknownCode, version is null ? $"There is no capability named \"{name}\"." : $"There is no capability \"{name}\" v{version}.");

    internal static Error Forbidden(string name) => Error.Forbidden(ForbiddenCode, $"The caller may not invoke {name}.");

    internal static Error ConfirmationRequired(string name) =>
        Error.InvalidState(ConfirmationRequiredCode, $"{name} runs only after explicit confirmation: ask, then invoke it again confirmed.");

    internal static Error IdempotencyKeyRequired(string name) =>
        new(IdempotencyKeyRequiredCode, $"{name} requires an idempotency key of at most {CapabilityInvocation.MaxIdempotencyKeyLength} characters.", ErrorKind.Validation);

    internal static Error IdempotencyKeyReused(string name) =>
        Error.Conflict(IdempotencyKeyReusedCode, $"This idempotency key was already used for {name} with different input; use a new key.");

    internal static Error InvalidInput(string reason) =>
        new(InvalidInputCode, $"The input does not match the capability's schema: {reason}", ErrorKind.Validation);

    internal static Error InvalidOutput(string reason) =>
        Error.InvalidState(InvalidOutputCode, $"The capability's output does not match its schema: {reason}");

    internal static Error Crashed(Exception exception) =>
        Error.InvalidState(CrashedCode, $"The capability failed unexpectedly ({exception.GetType().Name}).");
}
