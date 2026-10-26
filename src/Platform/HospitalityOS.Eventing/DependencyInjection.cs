using HospitalityOS.Eventing.Contracts;
using HospitalityOS.Eventing.Envelopes;
using HospitalityOS.Eventing.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HospitalityOS.Eventing;

/// <summary>
/// Registers the event backbone. The host must bind <c>IIdGenerator</c> (event IDs).
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the event type registry, the envelope factory, and the event serializer (all stateless singletons).
    /// </summary>
    public static IServiceCollection AddEventing(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<EventTypeRegistry>();
        services.TryAddSingleton<EventEnvelopeFactory>();
        services.TryAddSingleton<EventSerializer>();

        return services;
    }
}
