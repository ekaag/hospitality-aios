using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Services.Services.Events;

/// <summary>
/// A service was created.
/// </summary>
public sealed record ServiceCreated(ServiceId ServiceId, PropertyId PropertyId, ServiceDetails Details, DateTimeOffset OccurredAt) : IDomainEvent;
