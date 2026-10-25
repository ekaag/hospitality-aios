using HospitalityOS.SharedKernel.Domain;

namespace HospitalityOS.Domain.Services.Services.Events;

/// <summary>
/// A service's definition changed. Carries the full new definition, not a diff.
/// </summary>
public sealed record ServiceUpdated(ServiceId ServiceId, ServiceDetails Details, DateTimeOffset OccurredAt) : IDomainEvent;
