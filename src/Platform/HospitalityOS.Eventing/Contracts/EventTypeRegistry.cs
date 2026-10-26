using System.Text.RegularExpressions;
using HospitalityOS.Contracts.Events;

namespace HospitalityOS.Eventing.Contracts;

/// <summary>
/// Every published payload type and the event type and version it carries, read from the payload type names
/// (<c>HumanActionCompletedV1</c> is <c>HumanActionCompleted</c> v1). A badly named payload or a duplicate fails
/// when the registry is built, so it fails at startup. Singleton.
/// </summary>
internal sealed partial class EventTypeRegistry
{
    private readonly Dictionary<Type, EventTypeName> namesByPayloadType = [];
    private readonly Dictionary<EventTypeName, Type> payloadTypesByName = [];

    public EventTypeRegistry()
        : this(typeof(IEventPayload).Assembly.GetTypes())
    {
    }

    internal EventTypeRegistry(IEnumerable<Type> candidateTypes)
    {
        foreach (Type payloadType in candidateTypes.Where(type => type.IsClass && !type.IsAbstract && typeof(IEventPayload).IsAssignableFrom(type)))
        {
            EventTypeName name = Parse(payloadType);

            if (!payloadTypesByName.TryAdd(name, payloadType))
            {
                throw new InvalidOperationException($"{payloadType.FullName} and {payloadTypesByName[name].FullName} both publish {name}.");
            }

            namesByPayloadType.Add(payloadType, name);
        }
    }

    public IReadOnlyCollection<EventTypeName> All => payloadTypesByName.Keys;

    public EventTypeName NameOf(Type payloadType) =>
        namesByPayloadType.TryGetValue(payloadType, out EventTypeName name)
            ? name
            : throw new InvalidOperationException($"{payloadType.FullName} is not a registered event payload.");

    public Type? PayloadTypeFor(string eventType, int version) => payloadTypesByName.GetValueOrDefault(new EventTypeName(eventType, version));

    private static EventTypeName Parse(Type payloadType)
    {
        Match match = PayloadTypeName().Match(payloadType.Name);

        return match.Success
            ? new EventTypeName(match.Groups["type"].Value, int.Parse(match.Groups["version"].Value, System.Globalization.CultureInfo.InvariantCulture))
            : throw new InvalidOperationException($"Event payload {payloadType.FullName} must be named <EventType>V<version>, e.g. HumanActionCompletedV1.");
    }

    [GeneratedRegex("^(?<type>[A-Z][A-Za-z0-9]*?)V(?<version>[1-9][0-9]*)$")]
    private static partial Regex PayloadTypeName();
}
