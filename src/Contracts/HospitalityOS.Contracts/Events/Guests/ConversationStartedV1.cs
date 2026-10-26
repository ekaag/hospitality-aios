using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Guests;

/// <summary>
/// Version 1 of <c>ConversationStarted</c>: a guest conversation started.
/// </summary>
/// <param name="ConversationId">The conversation.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="Channel">The channel (domain enum name).</param>
public sealed record ConversationStartedV1(
    ConversationId ConversationId,
    PropertyId PropertyId,
    string Channel) : IEventPayload;
