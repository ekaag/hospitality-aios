using HospitalityOS.Application.Auditing;
using HospitalityOS.Bindings;
using HospitalityOS.Capabilities.Activities;
using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Capabilities.Dining;
using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Capabilities.GuestService;
using HospitalityOS.Capabilities.HumanActions;
using HospitalityOS.Capabilities.Properties;
using HospitalityOS.Capabilities.Registry;
using HospitalityOS.Capabilities.Retrieval;
using HospitalityOS.Domain.Capabilities.CapabilityInvocations;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Inference;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.Ports.Search;
using HospitalityOS.Ports.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HospitalityOS.Capabilities;

/// <summary>
/// Registers the capability registry, the executor, and the capabilities. The host must register persistence (unit of
/// work, invocation repository, audit log), <see cref="TenantContext"/> scoped, the application services (audit trail,
/// operation context), and bind <c>IIdGenerator</c>.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers <see cref="CapabilityRegistry"/> over every capability added with <see cref="AddCapability{TCapability}"/>,
    /// and the scoped <see cref="ICapabilityExecutor"/>.
    /// </summary>
    public static IServiceCollection AddCapabilities(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<CapabilityRegistry>();
        services.TryAddScoped<ICapabilityExecutor, CapabilityExecutor>();
        services.TryAddSingleton<Func<CapabilityScope>>(provider => () =>
        {
            AsyncServiceScope scope = provider.CreateAsyncScope();
            IServiceProvider scoped = scope.ServiceProvider;

            return new CapabilityScope(
                definition => scoped.GetRequiredKeyedService<ICapabilityInvoker>(definition.Key),
                scoped.GetRequiredService<TenantContext>(),
                scoped.GetRequiredService<OperationContext>(),
                scoped.GetRequiredService<IUnitOfWork>(),
                scoped.GetRequiredService<ICapabilityInvocationRepository>(),
                scoped.GetRequiredService<IAuditTrail>(),
                scope);
        });

        return services;
    }

    /// <summary>
    /// Registers the platform's own capabilities: the property, dining, and events reads (spec §11), with content search
    /// over the search index and vector store (<see cref="IContentIndexer"/> keeps them current) and the
    /// <c>content.embed</c> inference task (<see cref="IEmbeddingModel"/>). The host must register persistence for their
    /// repositories, bind <see cref="ISearchIndex"/> and <see cref="IVectorStore"/>, and route <c>content.embed</c>.
    /// </summary>
    public static IServiceCollection AddBuiltInCapabilities(this IServiceCollection services)
    {
        services.AddOptions<ContentSearchOptions>();
        services.TryAddScoped<ContentSearch>();
        services.TryAddScoped<IContentIndexer, ContentIndexer>();
        services.AddInferenceTask<IEmbeddingModel>(ContentIndex.EmbeddingTask);

        return services
            .AddCapability<GetPropertyCapability>()
            .AddCapability<GetOperatingHoursCapability>()
            .AddCapability<GetPolicyCapability>()
            .AddCapability<SearchContentCapability>()
            .AddCapability<SearchRestaurantsCapability>()
            .AddCapability<GetMenuCapability>()
            .AddCapability<SearchPropertyEventsCapability>()
            .AddCapability<CreateRequestCapability>()
            .AddCapability<GetRequestStatusCapability>()
            .AddCapability<CreateActionCapability>()
            .AddCapability<AssignActionCapability>()
            .AddCapability<UpdateActionStatusCapability>()
            .AddCapability<CompleteActionCapability>();
    }

    /// <summary>
    /// Registers <typeparamref name="TCapability"/>, scoped. Its definition is built and checked here, so an invalid
    /// declaration, or a second registration of the same name and version, fails at startup.
    /// </summary>
    /// <exception cref="InvalidOperationException">The declaration is invalid or already registered.</exception>
    public static IServiceCollection AddCapability<TCapability>(this IServiceCollection services)
        where TCapability : class, ICapability
    {
        CapabilityDefinition definition = CapabilityDefinition.For<TCapability>();

        if (services.Any(descriptor => descriptor.ImplementationInstance is CapabilityDefinition existing
            && existing.Name == definition.Name
            && existing.Version == definition.Version))
        {
            throw new InvalidOperationException($"Capability {definition} is already registered; a name and version can be registered once.");
        }

        services.AddSingleton(definition);
        services.TryAddScoped<TCapability>();
        services.AddKeyedScoped(
            typeof(ICapabilityInvoker),
            definition.Key,
            typeof(CapabilityInvoker<,,>).MakeGenericType(typeof(TCapability), definition.InputType, definition.OutputType));

        return services;
    }
}
