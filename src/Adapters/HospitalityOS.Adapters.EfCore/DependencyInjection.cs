using HospitalityOS.Adapters.EfCore.Auditing;
using HospitalityOS.Adapters.EfCore.Eventing;
using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Adapters.EfCore.Repositories;
using HospitalityOS.Domain.Activities.PropertyEvents;
using HospitalityOS.Domain.Capabilities.CapabilityInvocations;
using HospitalityOS.Domain.Dining.Menus;
using HospitalityOS.Domain.Dining.Restaurants;
using HospitalityOS.Domain.Guests.Conversations;
using HospitalityOS.Domain.Guests.GuestMessages;
using HospitalityOS.Domain.Guests.Guests;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.Domain.Integrations.ExternalReferences;
using HospitalityOS.Domain.Integrations.IntegrationConfigurations;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Domain.Organizations.Organizations;
using HospitalityOS.Domain.Properties.AccommodationTypes;
using HospitalityOS.Domain.Properties.AccommodationUnits;
using HospitalityOS.Domain.Properties.Amenities;
using HospitalityOS.Domain.Properties.ContentItems;
using HospitalityOS.Domain.Properties.Hours;
using HospitalityOS.Domain.Properties.Policies;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.Domain.Properties.Spaces;
using HospitalityOS.Domain.Services.ServiceRequests;
using HospitalityOS.Domain.Services.Services;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HospitalityOS.Adapters.EfCore;

/// <summary>
/// Registers the provider-neutral half of EF Core persistence. Hosts do not call this directly: they call a provider's
/// registration (e.g. <c>AddSqlitePersistence</c>), which adds the database and then this. The host must also register
/// <c>ICurrentTenant</c>.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers <see cref="IUnitOfWork"/>, every repository, the event store, outbox, and inbox, and the audit log, scoped, over the
    /// provider's <c>DbContext</c>, so aggregates and events commit in one transaction.
    /// </summary>
    public static IServiceCollection AddEfCorePersistence(this IServiceCollection services)
    {
        services.TryAddScoped<EfUnitOfWork>();
        services.TryAddScoped<IUnitOfWork>(provider => provider.GetRequiredService<EfUnitOfWork>());
        services.TryAddScoped<IEventStore, EfEventStore>();
        services.TryAddScoped<IOutboxStore, EfOutboxStore>();
        services.TryAddScoped<IInboxStore, EfInboxStore>();
        services.TryAddScoped<IAuditLog, EfAuditLog>();

        services.TryAddScoped<IOrganizationRepository, EfOrganizationRepository>();
        services.TryAddScoped<IPropertyRepository, EfPropertyRepository>();
        services.TryAddScoped<ISpaceRepository, EfSpaceRepository>();
        services.TryAddScoped<IAccommodationTypeRepository, EfAccommodationTypeRepository>();
        services.TryAddScoped<IAccommodationUnitRepository, EfAccommodationUnitRepository>();
        services.TryAddScoped<IOperatingHoursRepository, EfOperatingHoursRepository>();
        services.TryAddScoped<IAmenityRepository, EfAmenityRepository>();
        services.TryAddScoped<IPolicyRepository, EfPolicyRepository>();
        services.TryAddScoped<IContentItemRepository, EfContentItemRepository>();
        services.TryAddScoped<IRestaurantRepository, EfRestaurantRepository>();
        services.TryAddScoped<IMenuRepository, EfMenuRepository>();
        services.TryAddScoped<IPropertyEventRepository, EfPropertyEventRepository>();
        services.TryAddScoped<IServiceRepository, EfServiceRepository>();
        services.TryAddScoped<IServiceRequestRepository, EfServiceRequestRepository>();
        services.TryAddScoped<IHumanActionRepository, EfHumanActionRepository>();
        services.TryAddScoped<IGuestRepository, EfGuestRepository>();
        services.TryAddScoped<IStayContextRepository, EfStayContextRepository>();
        services.TryAddScoped<IConversationRepository, EfConversationRepository>();
        services.TryAddScoped<IGuestMessageRepository, EfGuestMessageRepository>();
        services.TryAddScoped<ICapabilityInvocationRepository, EfCapabilityInvocationRepository>();
        services.TryAddScoped<IIntegrationConfigurationRepository, EfIntegrationConfigurationRepository>();
        services.TryAddScoped<IExternalReferenceRepository, EfExternalReferenceRepository>();

        return services;
    }
}
