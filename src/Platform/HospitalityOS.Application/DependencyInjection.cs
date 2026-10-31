using HospitalityOS.Application.Accommodations;
using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Amenities;
using HospitalityOS.Application.Auditing;
using HospitalityOS.Application.Content;
using HospitalityOS.Application.Conversations;
using HospitalityOS.Application.Events;
using HospitalityOS.Application.Hours;
using HospitalityOS.Application.Menus;
using HospitalityOS.Application.Organizations;
using HospitalityOS.Application.Policies;
using HospitalityOS.Application.Properties;
using HospitalityOS.Application.Restaurants;
using HospitalityOS.Application.Services;
using HospitalityOS.Application.Spaces;
using HospitalityOS.Application.Staff;
using HospitalityOS.Ports.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HospitalityOS.Application;

/// <summary>
/// Registers the application services. The host must register persistence (the audit log), <c>TenantContext</c>,
/// and bind <c>IIdGenerator</c>.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers <see cref="IAuditTrail"/>, the scope's <see cref="OperationContext"/>, and the administration
    /// services (<see cref="IOrganizationAdministration"/>, <see cref="IPropertyAdministration"/>,
    /// <see cref="ISpaceAdministration"/>, <see cref="IAmenityAdministration"/>, <see cref="IOperatingHoursAdministration"/>,
    /// <see cref="IPolicyAdministration"/>, <see cref="IContentAdministration"/>, <see cref="IRestaurantAdministration"/>,
    /// <see cref="IMenuAdministration"/>, <see cref="IPropertyEventAdministration"/>, <see cref="IServiceAdministration"/>,
    /// <see cref="IAccommodationAdministration"/>, <see cref="IStaffAdministration"/>), and the guest-facing <see cref="IGuestConversations"/>.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<OperationContext>();
        services.TryAddScoped<IAuditTrail, AuditTrail>();
        services.TryAddScoped<AuditedCommit>();
        services.TryAddScoped<RecordIds>();
        services.TryAddScoped<IOrganizationAdministration, OrganizationAdministration>();
        services.TryAddScoped<PropertyReferences>();
        services.TryAddScoped<IPropertyAdministration, PropertyAdministration>();
        services.TryAddScoped<ISpaceAdministration, SpaceAdministration>();
        services.TryAddScoped<IAmenityAdministration, AmenityAdministration>();
        services.TryAddScoped<IOperatingHoursAdministration, OperatingHoursAdministration>();
        services.TryAddScoped<IPolicyAdministration, PolicyAdministration>();
        services.TryAddScoped<IContentAdministration, ContentAdministration>();
        services.TryAddScoped<IRestaurantAdministration, RestaurantAdministration>();
        services.TryAddScoped<IMenuAdministration, MenuAdministration>();
        services.TryAddScoped<IPropertyEventAdministration, PropertyEventAdministration>();
        services.TryAddScoped<IServiceAdministration, ServiceAdministration>();
        services.TryAddScoped<IStaffAdministration, StaffAdministration>();
        services.TryAddScoped<IAccommodationAdministration, AccommodationAdministration>();
        services.TryAddScoped<IGuestConversations, GuestConversations>();

        return services;
    }
}
