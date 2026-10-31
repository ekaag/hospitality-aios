using HospitalityOS.Contracts.Events;
using HospitalityOS.Eventing.Contracts;
using HospitalityOS.Eventing.Dispatching;
using HospitalityOS.Eventing.Envelopes;
using HospitalityOS.Eventing.Handling;
using HospitalityOS.Eventing.Replay;
using HospitalityOS.Eventing.Scheduling;
using HospitalityOS.Eventing.Serialization;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.Ports.Tenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace HospitalityOS.Eventing;

/// <summary>
/// Registers the event backbone. The host must bind <c>IIdGenerator</c> (event IDs) and <c>IEventTransport</c>, register
/// persistence (event store, outbox, inbox), and register <see cref="TenantContext"/> scoped.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers envelopes and serialization, the <see cref="IEventMessageFactory"/> persistence calls on commit, the
    /// outbox dispatcher (configured from <c>Eventing:Dispatcher</c>, validated at startup), the subscriptions of every
    /// handler added with <see cref="AddEventHandler{TPayload, THandler}"/>, and <see cref="IEventReplayer"/>.
    /// </summary>
    public static IServiceCollection AddEventing(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<EventTypeRegistry>();
        services.TryAddSingleton<EventEnvelopeFactory>();
        services.TryAddSingleton<EventSerializer>();
        services.TryAddScoped<OperationContext>();
        services.TryAddScoped<IEventMessageFactory, EventMessageFactory>();

        services.AddOptions<EventDispatcherOptions>()
            .Bind(configuration.GetSection(EventDispatcherOptions.SectionName))
            .Validate(options => !options.Validate().Any(), "Invalid Eventing:Dispatcher settings.")
            .ValidateOnStart();
        services.TryAddSingleton<Func<DispatcherScope>>(provider => () =>
        {
            AsyncServiceScope scope = provider.CreateAsyncScope();

            return new DispatcherScope(scope.ServiceProvider.GetRequiredService<IOutboxStore>(), scope);
        });
        services.TryAddSingleton<OutboxDispatcher>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, EventDispatcherService>());

        services.TryAddSingleton<Func<string, HandlerScope>>(provider => handlerName =>
        {
            AsyncServiceScope scope = provider.CreateAsyncScope();
            IServiceProvider scoped = scope.ServiceProvider;

            return new HandlerScope(
                scoped.GetRequiredKeyedService<IEventHandlerInvoker>(handlerName),
                scoped.GetRequiredService<IInboxStore>(),
                scoped.GetRequiredService<IUnitOfWork>(),
                scoped.GetRequiredService<TenantContext>(),
                scoped.GetRequiredService<OperationContext>(),
                scope);
        });
        services.TryAddSingleton<EventProcessor>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, EventSubscriptionHost>());

        services.TryAddScoped<IWorkScheduler, WorkScheduler>();
        services.TryAddSingleton<Func<string, ScheduledScope>>(provider => handlerName =>
        {
            AsyncServiceScope scope = provider.CreateAsyncScope();
            IServiceProvider scoped = scope.ServiceProvider;

            return new ScheduledScope(
                () => scoped.GetRequiredKeyedService<IScheduledHandlerInvoker>(handlerName),
                scoped.GetRequiredService<IInboxStore>(),
                scoped.GetRequiredService<IUnitOfWork>(),
                scoped.GetRequiredService<TenantContext>(),
                scoped.GetRequiredService<OperationContext>(),
                scope);
        });
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ScheduledWorkHost>());

        services.TryAddSingleton<Func<ReplayScope>>(provider => () =>
        {
            AsyncServiceScope scope = provider.CreateAsyncScope();

            return new ReplayScope(scope.ServiceProvider.GetRequiredService<IEventStore>(), scope);
        });
        services.TryAddSingleton<IEventReplayer, EventReplayer>();

        return services;
    }

    /// <summary>
    /// Subscribes <typeparamref name="THandler"/> to <typeparamref name="TPayload"/> events under <paramref name="name"/>,
    /// its stable identity: the transport subscription and the inbox key. Renaming a handler makes it process past events
    /// again on replay, so keep names stable. A handler of several event types registers once per type, with distinct names.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">Stable, unique name, e.g. <c>GuestService.CompleteRequestOnHumanActionCompleted</c>.</param>
    public static IServiceCollection AddEventHandler<TPayload, THandler>(this IServiceCollection services, string name)
        where TPayload : IEventPayload
        where THandler : class, IEventHandler<TPayload>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, IInboxStore.MaxHandlerLength, nameof(name));

        if (services.Any(descriptor => descriptor.ImplementationInstance is EventHandlerRegistration existing && existing.Name == name))
        {
            throw new InvalidOperationException($"An event handler named '{name}' is already registered; handler names must be unique.");
        }

        services.AddSingleton(new EventHandlerRegistration(name, EventTypeRegistry.Parse(typeof(TPayload))));
        services.TryAddScoped<THandler>();
        services.AddKeyedScoped<IEventHandlerInvoker, EventHandlerInvoker<TPayload, THandler>>(name);

        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="THandler"/> to run work scheduled for <paramref name="name"/> through
    /// <see cref="IWorkScheduler"/>, scoped, acting as the agent <paramref name="name"/>. The host must bind
    /// <c>IScheduler</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">A scheduled handler named <paramref name="name"/> is already registered.</exception>
    public static IServiceCollection AddScheduledHandler<TPayload, THandler>(this IServiceCollection services, string name)
        where THandler : class, IScheduledHandler<TPayload>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, IInboxStore.MaxHandlerLength, nameof(name));

        if (services.Any(descriptor => descriptor.ImplementationInstance is ScheduledHandlerRegistration existing && existing.Name == name))
        {
            throw new InvalidOperationException($"A scheduled handler named '{name}' is already registered; handler names must be unique.");
        }

        services.AddSingleton(new ScheduledHandlerRegistration(name, typeof(TPayload)));
        services.TryAddScoped<THandler>();
        services.AddKeyedScoped<IScheduledHandlerInvoker, ScheduledHandlerInvoker<TPayload, THandler>>(name);

        return services;
    }
}
