using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Conversations;

/// <summary>
/// A guest's conversation with the property.
/// </summary>
/// <param name="Id">Platform-owned ID.</param>
/// <param name="PropertyId">The property the guest is talking to.</param>
/// <param name="StayContextId">The guest's stay, if linked.</param>
/// <param name="Status">Whether it accepts messages.</param>
/// <param name="StartedAt">When it started (UTC).</param>
public sealed record ConversationResponse(
    ConversationId Id,
    PropertyId PropertyId,
    StayContextId? StayContextId,
    ConversationStatus Status,
    DateTimeOffset StartedAt);
