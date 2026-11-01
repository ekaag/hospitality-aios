using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace HospitalityOS.Bindings.Inference;

/// <summary>
/// Inference metrics under <see cref="Name"/>: each provider attempt by task, provider, model, and outcome
/// (<c>succeeded</c> or <c>unavailable</c>, the latter falling back), with its duration, so a provider outage shows as
/// fallbacks before it shows as failures. Singleton.
/// </summary>
internal sealed class InferenceMetrics
{
    public const string Name = "HospitalityOS.Inference";

    private readonly Counter<long> attempts;
    private readonly Histogram<double> duration;

    public InferenceMetrics(IMeterFactory? meters = null)
    {
        Meter meter = meters?.Create(Name) ?? new Meter(Name);

        attempts = meter.CreateCounter<long>("hospitalityos.inference.attempts", "{attempt}", "Calls to a provider for a task, by outcome.");
        duration = meter.CreateHistogram<double>("hospitalityos.inference.duration", "s", "How long a provider took to answer, or to fail.");
    }

    public void Attempted(InferenceAttemptRecord attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        TagList tags = new() { { "task", attempt.Task }, { "provider", attempt.Provider }, { "model", attempt.Model }, { "outcome", attempt.Succeeded ? "succeeded" : "unavailable" } };

        attempts.Add(1, tags);
        duration.Record(attempt.Elapsed.TotalSeconds, tags);
    }
}

/// <summary>
/// One provider attempt, as measured.
/// </summary>
internal sealed record InferenceAttemptRecord(string Task, string Provider, string Model, bool Succeeded, TimeSpan Elapsed);
