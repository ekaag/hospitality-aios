using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Guests.Conversations.Events;

/// <summary>
/// A conversation was started.
/// </summary>
public sealed record ConversationStarted(ConversationId ConversationId, PropertyId PropertyId, ConversationChannel Channel, DateTimeOffset OccurredAt) : IDomainEvent;
