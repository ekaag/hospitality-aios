namespace HospitalityOS.SharedKernel.Domain;

/// <summary>
/// A state change raised by an aggregate. Named <c>&lt;Aggregate&gt;&lt;Verb&gt;</c> in the past tense,
/// e.g. <c>HumanActionCompleted</c>. The outbox wraps it in the canonical envelope (P1).
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// When the change happened (UTC), taken from the injected <see cref="TimeProvider"/>.
    /// </summary>
    DateTimeOffset OccurredAt { get; }
}
