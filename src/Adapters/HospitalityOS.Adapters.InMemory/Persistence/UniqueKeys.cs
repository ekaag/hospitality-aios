using HospitalityOS.Domain.Guests.GuestMessages;
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
    ];

    private static readonly ILookup<Type, UniqueKey> byType = all.ToLookup(key => key.AggregateType);

    public static IEnumerable<UniqueKey> For(Type aggregateType) => byType[aggregateType];

    private static UniqueKey Key<TAggregate>(string name, Func<TAggregate, object?> select)
        where TAggregate : IAggregateRoot
    {
        return new UniqueKey(typeof(TAggregate), name, aggregate => select((TAggregate)aggregate));
    }
}
