using HospitalityOS.SharedKernel.Domain;

namespace HospitalityOS.Domain.Properties.ContentItems.Events;

/// <summary>
/// A content item's text or status changed; search indexes re-index on this (G5). Carries the full new state.
/// </summary>
public sealed record ContentItemUpdated(
    ContentItemId ContentItemId,
    ContentItemDetails Details,
    ContentStatus Status,
    DateTimeOffset OccurredAt) : IDomainEvent;
