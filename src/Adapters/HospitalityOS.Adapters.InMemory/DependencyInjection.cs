using HospitalityOS.Adapters.InMemory.Auditing;
using HospitalityOS.Adapters.InMemory.Eventing;
using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Adapters.InMemory.Repositories;
using HospitalityOS.Domain.Activities.PropertyEvents;
using HospitalityOS.Domain.Capabilities.AITraces;
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

namespace HospitalityOS.Adapters.InMemory;

/// <summary>
/// Registers pure in-memory persistence (no EF): for local development and fast tests. Data lives as long as
/// the container. The host must also register <c>ICurrentTenant</c>.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers <see cref="IUnitOfWork"/>, every repository, the event store, outbox, and inbox, and the audit log, scoped, over one
    /// singleton store, so aggregates and events commit together.
    /// </summary>
    public static IServiceCollection AddInMemoryPersistence(this IServiceCollection services)
    {
        services.TryAddSingleton<InMemoryStore>();
        services.TryAddScoped<InMemoryUnitOfWork>();
        services.TryAddScoped<IUnitOfWork>(provider => provider.GetRequiredService<InMemoryUnitOfWork>());
        services.TryAddScoped<IEventStore, InMemoryEventStore>();
        services.TryAddScoped<IOutboxStore, InMemoryOutboxStore>();
        services.TryAddScoped<IInboxStore, InMemoryInboxStore>();
        services.TryAddScoped<IAuditLog, InMemoryAuditLog>();

        services.TryAddScoped<IOrganizationRepository, InMemoryOrganizationRepository>();
        services.TryAddScoped<IPropertyRepository, InMemoryPropertyRepository>();
        services.TryAddScoped<ISpaceRepository, InMemorySpaceRepository>();
        services.TryAddScoped<IAccommodationTypeRepository, InMemoryAccommodationTypeRepository>();
        services.TryAddScoped<IAccommodationUnitRepository, InMemoryAccommodationUnitRepository>();
        services.TryAddScoped<IOperatingHoursRepository, InMemoryOperatingHoursRepository>();
        services.TryAddScoped<IAmenityRepository, InMemoryAmenityRepository>();
        services.TryAddScoped<IPolicyRepository, InMemoryPolicyRepository>();
        services.TryAddScoped<IContentItemRepository, InMemoryContentItemRepository>();
        services.TryAddScoped<IRestaurantRepository, InMemoryRestaurantRepository>();
        services.TryAddScoped<IMenuRepository, InMemoryMenuRepository>();
        services.TryAddScoped<IPropertyEventRepository, InMemoryPropertyEventRepository>();
        services.TryAddScoped<IServiceRepository, InMemoryServiceRepository>();
        services.TryAddScoped<IServiceRequestRepository, InMemoryServiceRequestRepository>();
        services.TryAddScoped<IHumanActionRepository, InMemoryHumanActionRepository>();
        services.TryAddScoped<IGuestRepository, InMemoryGuestRepository>();
        services.TryAddScoped<IStayContextRepository, InMemoryStayContextRepository>();
        services.TryAddScoped<IConversationRepository, InMemoryConversationRepository>();
        services.TryAddScoped<IGuestMessageRepository, InMemoryGuestMessageRepository>();
        services.TryAddScoped<ICapabilityInvocationRepository, InMemoryCapabilityInvocationRepository>();
        services.TryAddScoped<IAITraceRepository, InMemoryAITraceRepository>();
        services.TryAddScoped<IIntegrationConfigurationRepository, InMemoryIntegrationConfigurationRepository>();
        services.TryAddScoped<IExternalReferenceRepository, InMemoryExternalReferenceRepository>();

        return services;
    }
}
