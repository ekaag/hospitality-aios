using HospitalityOS.Contracts.Events;

namespace HospitalityOS.Eventing.Envelopes;

/// <summary>
/// Where an event came from: the flow it belongs to, what directly caused it, and who acted. The ambient
/// execution context (C7) supplies it; until then callers pass it explicitly.
/// </summary>
/// <param name="CorrelationId">The end-to-end flow.</param>
/// <param name="CausationId">The event or command that directly caused this one, if any.</param>
/// <param name="ActorType">Who acted.</param>
/// <param name="ActorId">The actor's identifier, if known.</param>
internal sealed record EventOrigin(Guid CorrelationId, Guid? CausationId, ActorType ActorType, string? ActorId);
