using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Properties.Hours;
using HospitalityOS.Domain.Properties.Shared;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfOperatingHoursRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<OperatingHours, OperatingHoursId>(context, unitOfWork, currentTenant), IOperatingHoursRepository
{
    public Task<OperatingHours?> FindBySubjectAsync(PropertyId propertyId, SubjectReference subject, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subject);

        string key = KeyColumns.Subject(subject);

        return FindFirstAsync(
            hours => hours.PropertyId == propertyId && EF.Property<string>(hours, KeyColumns.HoursSubjectKey) == key,
            cancellationToken);
    }
}
