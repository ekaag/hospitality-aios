using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Persistence;

namespace HospitalityOS.PortContractTests.Persistence.Support;

/// <summary>
/// Records committed events with a predictable shape: the domain event's type name as event type, one shared
/// correlation, and sequential IDs. The real envelope belongs to the event backbone, not to persistence.
/// </summary>
internal sealed class TestEventMessageFactory : IEventMessageFactory
{
    public static Guid Correlation { get; } = TestData.Id(950_000);

    private static int sequence;

    public EventMessage Create(CommittedEvent committed) => new(
        TestData.Id(960_000 + Interlocked.Increment(ref sequence)),
        committed.Event.GetType().Name,
        1,
        committed.TenantId,
        committed.AggregateType,
        committed.AggregateId,
        Correlation,
        committed.Event.OccurredAt,
        "{}");
}
