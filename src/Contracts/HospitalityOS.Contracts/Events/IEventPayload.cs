namespace HospitalityOS.Contracts.Events;

/// <summary>
/// The published body of one event type at one version, carried in <see cref="EventEnvelope.Payload"/>. The type
/// name is the contract: <c>&lt;EventType&gt;V&lt;version&gt;</c>, e.g. <c>HumanActionCompletedV1</c> is
/// <c>HumanActionCompleted</c> version 1. A published version never changes; a breaking change adds <c>V2</c>
/// alongside it so existing consumers keep working.
/// </summary>
/// <remarks>
/// Payloads use platform types only: shared IDs from SharedKernel, other IDs as <see cref="Guid"/>, and enums as
/// their domain names, so consumers never depend on a domain module's internals (P9). Guest personal data never
/// appears in a payload (P10).
/// </remarks>
public interface IEventPayload;
