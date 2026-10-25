using HospitalityOS.SharedKernel.Domain;

namespace HospitalityOS.UnitTests.SharedKernel.TestDoubles;

internal sealed record SampleRenamed(SampleId SampleId, string Name, DateTimeOffset OccurredAt) : IDomainEvent;
