using System.Reflection;
using System.Text.Json;
using HospitalityOS.Contracts.Events.Organizations;
using HospitalityOS.Contracts.Events.Properties;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Domain.Activities.PropertyEvents;
using HospitalityOS.Domain.Dining.Menus;
using HospitalityOS.Domain.Guests.Guests;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Domain.Organizations.Organizations;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.Domain.Services.Services;
using HospitalityOS.Eventing.Contracts;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.UnitTests.Eventing.TestDoubles;
using Xunit;

namespace HospitalityOS.UnitTests.Eventing.Contracts;

public sealed class EventContractMapperTests
{
    /// <summary>
    /// Set to <c>1</c> to rewrite the sample files after a deliberate contract change; review the diff before committing.
    /// </summary>
    private const string UpdateSamplesVariable = "HOSPITALITYOS_UPDATE_EVENT_SAMPLES";

    private static readonly Assembly[] domainAssemblies =
    [
        typeof(Organization).Assembly,
        typeof(Property).Assembly,
        typeof(Menu).Assembly,
        typeof(PropertyEvent).Assembly,
        typeof(Service).Assembly,
        typeof(HumanAction).Assembly,
        typeof(Guest).Assembly,
    ];

    private static readonly JsonSerializerOptions sampleFormat = new(ContractJson.Options) { WriteIndented = true, NewLine = "\n" };

    [Fact]
    public void Samples_CoverEveryDomainEvent()
    {
        IEnumerable<string> domainEvents = domainAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsClass && !type.IsAbstract && typeof(IDomainEvent).IsAssignableFrom(type))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal);

        IEnumerable<string> sampled = DomainEventSamples.All.Select(domainEvent => domainEvent.GetType().Name).Order(StringComparer.Ordinal);

        Assert.Equal(domainEvents, sampled);
    }

    [Theory]
    [MemberData(nameof(DomainEventSamples.Names), MemberType = typeof(DomainEventSamples))]
    public void Map_EveryDomainEvent_PublishesVersion1OfTheSameEventType(string domainEvent)
    {
        MappedEvent mapped = EventContractMapper.Map(DomainEventSamples.Get(domainEvent));

        Assert.Equal(new EventTypeName(domainEvent, 1), new EventTypeRegistry().NameOf(mapped.Payload.GetType()));
    }

    [Theory]
    [MemberData(nameof(DomainEventSamples.Names), MemberType = typeof(DomainEventSamples))]
    public void Map_EveryDomainEvent_MatchesItsPublishedSample(string domainEvent)
    {
        MappedEvent mapped = EventContractMapper.Map(DomainEventSamples.Get(domainEvent));
        string actual = JsonSerializer.Serialize(mapped.Payload, mapped.Payload.GetType(), sampleFormat) + "\n";
        string path = SamplePath(domainEvent);

        if (Environment.GetEnvironmentVariable(UpdateSamplesVariable) == "1")
        {
            File.WriteAllText(path, actual);
        }

        Assert.True(File.Exists(path), $"No published sample for {domainEvent}; run the tests with {UpdateSamplesVariable}=1 and review the new file.");
        Assert.Equal(File.ReadAllText(path).ReplaceLineEndings("\n"), actual);
    }

    [Fact]
    public void Map_OrganizationAndPropertyCreation_IdentifyTheOrganization()
    {
        MappedEvent organization = EventContractMapper.Map(DomainEventSamples.Get("OrganizationCreated"));
        MappedEvent property = EventContractMapper.Map(DomainEventSamples.Get("PropertyCreated"));

        Assert.Equal(((OrganizationCreatedV1)organization.Payload).OrganizationId, organization.OrganizationId);
        Assert.Equal(((PropertyCreatedV1)property.Payload).OrganizationId, property.OrganizationId);
    }

    [Fact]
    public void Map_UnknownDomainEvent_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => EventContractMapper.Map(new UnpublishedEvent(DomainEventSamples.Now)));
    }

    private static string SamplePath(string domainEvent)
    {
        DirectoryInfo directory = new(AppContext.BaseDirectory);

        while (!File.Exists(Path.Combine(directory.FullName, "HospitalityOS.slnx")))
        {
            directory = directory.Parent ?? throw new InvalidOperationException("Repository root not found.");
        }

        return Path.Combine(directory.FullName, "tests", "HospitalityOS.UnitTests", "Eventing", "Contracts", "Samples", $"{domainEvent}.v1.json");
    }

    private sealed record UnpublishedEvent(DateTimeOffset OccurredAt) : IDomainEvent;
}
