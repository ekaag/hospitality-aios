using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Tenancy;
using HospitalityOS.UnitTests.SharedKernel.TestDoubles;
using Xunit;

namespace HospitalityOS.UnitTests.SharedKernel.Domain;

public sealed class AggregateRootTests
{
    private static readonly SampleId id = new(Guid.Parse("01890a5d-ac96-774b-bcce-b302099a8057"));
    private static readonly TenantId tenantId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b302099a8059"));
    private static readonly DateTimeOffset now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DomainEvents_WhenNew_IsEmpty()
    {
        SampleAggregate aggregate = new(id, tenantId);

        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void Raise_WhenStateChanges_RecordsEventsInOrder()
    {
        SampleAggregate aggregate = new(id, tenantId);

        aggregate.Rename("Pool", now);
        aggregate.Rename("Rooftop Pool", now.AddMinutes(1));

        Assert.Equal(
            [new SampleRenamed(id, "Pool", now), new SampleRenamed(id, "Rooftop Pool", now.AddMinutes(1))],
            aggregate.DomainEvents);
    }

    [Fact]
    public void Version_AdvancesOncePerRaisedEvent_AndSurvivesDequeue()
    {
        SampleAggregate aggregate = new(id, tenantId);

        aggregate.Rename("Pool", now);
        aggregate.Rename("Rooftop Pool", now);
        aggregate.DequeueDomainEvents();
        aggregate.Rename(" ", now);

        Assert.Equal(2, aggregate.Version);
    }

    [Fact]
    public void Raise_WhenOperationFails_RecordsNoEvent()
    {
        SampleAggregate aggregate = new(id, tenantId);

        aggregate.Rename(" ", now);

        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void DequeueDomainEvents_ReturnsEventsAndClears()
    {
        SampleAggregate aggregate = new(id, tenantId);
        aggregate.Rename("Pool", now);

        IReadOnlyList<IDomainEvent> dequeued = aggregate.DequeueDomainEvents();

        Assert.Equal([new SampleRenamed(id, "Pool", now)], dequeued);
        Assert.Empty(aggregate.DomainEvents);
        Assert.Empty(aggregate.DequeueDomainEvents());
    }

    [Fact]
    public void DequeueDomainEvents_ThroughNonGenericView_ReturnsEvents()
    {
        SampleAggregate aggregate = new(id, tenantId);
        aggregate.Rename("Pool", now);
        // Mirrors how persistence will collect events from tracked aggregates of any type.
        IAggregateRoot[] tracked = [aggregate, new OtherSampleAggregate(id)];

        IDomainEvent[] dequeued = [.. tracked.SelectMany(view => view.DequeueDomainEvents())];

        Assert.Equal([new SampleRenamed(id, "Pool", now)], dequeued);
    }
}
