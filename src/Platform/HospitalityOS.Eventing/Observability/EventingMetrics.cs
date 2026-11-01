using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace HospitalityOS.Eventing.Observability;

/// <summary>
/// Event processing metrics (spec §37) and spans, under <see cref="Name"/>: what the dispatcher published, retried, and
/// dead-lettered, and what each handler handled, skipped as a duplicate, or failed, with how long handling took and how
/// long after recording it ran. Tagged by event type and handler only, never by tenant or IDs, so series stay few.
/// Singleton.
/// </summary>
internal sealed class EventingMetrics
{
    public const string Name = "HospitalityOS.Eventing";

    private readonly Counter<long> published;
    private readonly Counter<long> publishFailures;
    private readonly Counter<long> handled;
    private readonly Histogram<double> handlingDuration;
    private readonly Histogram<double> deliveryDelay;

    public EventingMetrics(IMeterFactory? meters = null)
    {
        Meter meter = meters?.Create(Name) ?? new Meter(Name);

        published = meter.CreateCounter<long>("hospitalityos.events.published", "{event}", "Events published to the transport.");
        publishFailures = meter.CreateCounter<long>("hospitalityos.events.publish.failures", "{event}", "Failed publishes, by whether the event is retried or dead-lettered.");
        handled = meter.CreateCounter<long>("hospitalityos.events.handled", "{delivery}", "Deliveries processed by a handler, by outcome: handled, duplicate, or failed.");
        handlingDuration = meter.CreateHistogram<double>("hospitalityos.events.handling.duration", "s", "How long a handler took with a delivery.");
        deliveryDelay = meter.CreateHistogram<double>("hospitalityos.events.delivery.delay", "s", "How long after it was recorded a handler finished an event.");
    }

    public static ActivitySource Source { get; } = new(Name);

    public void Published(string eventType) => published.Add(1, new KeyValuePair<string, object?>("event.type", eventType));

    public void PublishFailed(string eventType, bool deadLettered) =>
        publishFailures.Add(1, new KeyValuePair<string, object?>("event.type", eventType), new KeyValuePair<string, object?>("outcome", deadLettered ? "dead_letter" : "retry"));

    public void Handled(string handler, string eventType, string outcome, TimeSpan duration, TimeSpan sinceRecorded)
    {
        TagList tags = new() { { "handler", handler }, { "event.type", eventType }, { "outcome", outcome } };

        handled.Add(1, tags);
        handlingDuration.Record(duration.TotalSeconds, tags);

        if (outcome == HandledOutcome)
        {
            deliveryDelay.Record(Math.Max(0, sinceRecorded.TotalSeconds), new KeyValuePair<string, object?>("handler", handler), new KeyValuePair<string, object?>("event.type", eventType));
        }
    }

    public const string HandledOutcome = "handled";
    public const string DuplicateOutcome = "duplicate";
    public const string FailedOutcome = "failed";
}
