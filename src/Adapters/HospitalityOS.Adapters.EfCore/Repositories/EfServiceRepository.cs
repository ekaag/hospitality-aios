using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Services.Services;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfServiceRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<Service, ServiceId>(context, unitOfWork, currentTenant), IServiceRepository
{
    public Task<Service?> FindByCodeAsync(PropertyId propertyId, string code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        string key = KeyColumns.ServiceCode(code);

        return FindFirstAsync(
            service => service.PropertyId == propertyId && EF.Property<string>(service, KeyColumns.ServiceCodeKey) == key,
            cancellationToken);
    }
}
