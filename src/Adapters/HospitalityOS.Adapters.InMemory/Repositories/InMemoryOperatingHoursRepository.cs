using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Properties.Hours;
using HospitalityOS.Domain.Properties.Shared;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryOperatingHoursRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<OperatingHours, OperatingHoursId>(store, unitOfWork, currentTenant), IOperatingHoursRepository
{
    public Task<OperatingHours?> FindBySubjectAsync(PropertyId propertyId, SubjectReference subject, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subject);

        return FindFirstAsync(hours => hours.PropertyId == propertyId && hours.Subject == subject, cancellationToken);
    }
}
