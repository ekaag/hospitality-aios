using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Properties.ContentItems.Events;

/// <summary>
/// A content item was created, as a draft.
/// </summary>
public sealed record ContentItemCreated(
    ContentItemId ContentItemId,
    PropertyId PropertyId,
    ContentItemDetails Details,
    ContentStatus Status,
    DateTimeOffset OccurredAt) : IDomainEvent;
