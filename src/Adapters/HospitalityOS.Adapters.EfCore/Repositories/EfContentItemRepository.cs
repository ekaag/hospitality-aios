using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Properties.ContentItems;
using HospitalityOS.Ports.Tenancy;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfContentItemRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<ContentItem, ContentItemId>(context, unitOfWork, currentTenant), IContentItemRepository;
