using HospitalityOS.Domain.Capabilities.CapabilityInvocations;
using HospitalityOS.Domain.Guests.GuestMessages;
using HospitalityOS.Domain.Integrations.ExternalReferences;
using HospitalityOS.Domain.Integrations.IntegrationConfigurations;
using HospitalityOS.Domain.Properties.AccommodationUnits;
using HospitalityOS.Domain.Properties.Hours;
using HospitalityOS.Domain.Services.ServiceRequests;
using HospitalityOS.Domain.Services.Services;
using HospitalityOS.SharedKernel.Domain;

namespace HospitalityOS.Adapters.InMemory.Persistence;

/// <summary>
/// The unique indexes the domain relies on, mirroring the database indexes (C2). The repositories' <c>Find…</c>
/// checks catch ordinary duplicates; these catch two scopes racing past the check (P8).
/// </summary>
internal static class UniqueKeys
{
    private static readonly UniqueKey[] all =
    [
        Key<AccommodationUnit>("unitNumber", unit => (unit.PropertyId, unit.UnitNumber.ToUpperInvariant())),
        Key<Service>("code", service => (service.PropertyId, service.Details.Code.ToUpperInvariant())),
        Key<OperatingHours>("subject", hours => (hours.PropertyId, hours.Subject)),
        Key<GuestMessage>("clientMessageId", message => message.ClientMessageId is { } clientId ? (message.ConversationId, clientId) : null),
        Key<ServiceRequest>("sourceMessageId", request => request.Origin.SourceMessageId),
        Key<IntegrationConfiguration>("kind", configuration => (configuration.PropertyId, configuration.Kind)),
        Key<ExternalReference>("externalId", reference => (reference.PropertyId, reference.ProviderName, reference.EntityType, reference.ExternalId)),

        // Only succeeded invocations hold their key, so a failed attempt can be retried with it.
        Key<CapabilityInvocation>(
            "succeededIdempotencyKey",
            invocation => invocation is { Status: CapabilityInvocationStatus.Succeeded, IdempotencyKey: { } key } ? (invocation.TenantId, invocation.CapabilityName, key) : null),
    ];

    private static readonly ILookup<Type, UniqueKey> byType = all.ToLookup(key => key.AggregateType);

    public static IEnumerable<UniqueKey> For(Type aggregateType) => byType[aggregateType];

    private static UniqueKey Key<TAggregate>(string name, Func<TAggregate, object?> select)
        where TAggregate : IAggregateRoot
    {
        return new UniqueKey(typeof(TAggregate), name, aggregate => select((TAggregate)aggregate));
    }
}
