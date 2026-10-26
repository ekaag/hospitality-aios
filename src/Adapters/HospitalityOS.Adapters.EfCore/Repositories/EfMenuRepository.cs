using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Dining.Menus;
using HospitalityOS.Ports.Tenancy;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfMenuRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<Menu, MenuId>(context, unitOfWork, currentTenant), IMenuRepository;
