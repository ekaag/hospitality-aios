using HospitalityOS.Eventing.Contracts;
using HospitalityOS.Eventing.Dispatching;
using HospitalityOS.Eventing.Envelopes;
using HospitalityOS.Eventing.Serialization;
using HospitalityOS.Ports.Eventing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace HospitalityOS.Eventing;

/// <summary>
/// Registers the event backbone. The host must bind <c>IIdGenerator</c> (event IDs) and <c>IEventTransport</c>, and
/// register persistence (event store and outbox).
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers envelopes and serialization, the <see cref="IEventMessageFactory"/> persistence calls on commit, and the
    /// outbox dispatcher as a hosted service configured from <c>Eventing:Dispatcher</c> (validated at startup).
    /// </summary>
    public static IServiceCollection AddEventing(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<EventTypeRegistry>();
        services.TryAddSingleton<EventEnvelopeFactory>();
        services.TryAddSingleton<EventSerializer>();
        services.TryAddScoped<ScopedEventOrigin>();
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

        return services;
    }
}
